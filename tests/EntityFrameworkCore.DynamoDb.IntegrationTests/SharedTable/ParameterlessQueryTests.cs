using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>
///     Regression tests for the bug where ExecuteStatementRequest.Parameters was set to an empty
///     list when a PartiQL query contained no parameterized placeholders, causing real DynamoDB to
///     reject the request with a validation error (Members must have length >= 1). DynamoDB Local
///     is lenient about this; the tests below use a mock client to verify the actual request shape.
/// </summary>
public class ParameterlessQueryTests
{
    private static IAmazonDynamoDB CreateMockClient(List<ExecuteStatementRequest> captured)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(captured.Add),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new ExecuteStatementResponse { Items = [] }));
        return client;
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_SharedTableWithDiscriminator_SendsNullParameters()
    {
        var captured = new List<ExecuteStatementRequest>();
        await using var ctx = SharedTableDbContext.Create(CreateMockClient(captured));

        // Produces: WHERE "pk" = 'TENANT#U' AND "$type" = 'UserEntity' — all literals, no ?
        _ = await ctx.Users.Where(u => u.Pk == "TENANT#U").ToListAsync();

        captured.Should().ContainSingle();
        captured[0].Parameters.Should().BeNullOrEmpty(
            "DynamoDB rejects Parameters = [] — must be null or absent when there are no placeholders");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FirstOrDefaultAsync_SharedTableWithDiscriminator_SendsNullParameters()
    {
        var captured = new List<ExecuteStatementRequest>();
        await using var ctx = SharedTableDbContext.Create(CreateMockClient(captured));

        _ = await ctx.Users.Where(u => u.Pk == "TENANT#U" && u.Sk == "USER#1").FirstOrDefaultAsync();

        captured.Should().ContainSingle();
        captured[0].Parameters.Should().BeNullOrEmpty();
    }
}
