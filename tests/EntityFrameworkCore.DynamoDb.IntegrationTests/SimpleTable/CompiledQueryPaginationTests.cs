using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for compiled queries using Limit(n) with runtime parameters.</summary>
public class CompiledQueryPaginationTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    // Parameterized Limit(n): limit = n + 1 evaluated at runtime.
    private static readonly Func<SimpleTableDbContext, int, IAsyncEnumerable<SimpleItem>>
        LimitPlusOneQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, int n)
            => ctx.SimpleItems.Limit(n + 1));

    [Fact]
    public async Task Limit_WithArithmetic_EvaluatesAtRuntime()
    {
        SqlCapture.Clear();

        // n=4 → Limit=5; DynamoDB evaluates 5 items.
        var results = await LimitPlusOneQuery(Db, 4).ToListAsync(CancellationToken);

        // At most 5 items evaluated and returned.
        results.Count.Should().BeLessThanOrEqualTo(5);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(5);
    }
}
