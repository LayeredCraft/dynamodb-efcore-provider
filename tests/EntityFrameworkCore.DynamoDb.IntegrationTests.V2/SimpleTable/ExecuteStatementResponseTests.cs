using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

public class ExecuteStatementResponseTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    [Fact]
    public async Task FirstAsync_PopulatesResponseOnEntry()
    {
        var item = await Db.SimpleItems.Where(x => x.Pk == "ITEM#1").FirstAsync(CancellationToken);

        var response = Db.Entry(item).GetExecuteStatementResponse();

        response.Should().NotBeNull();
        response!
            .ResponseMetadata
            .RequestId
            .Should()
            .NotBeNullOrEmpty("RequestId is always populated by DynamoDB");
    }

    [Fact]
    public async Task ToListAsync_AllEntitiesFromSinglePage_ShareSameResponseReference()
    {
        var items = await Db.SimpleItems.ToListAsync(CancellationToken);
        items.Should().HaveCountGreaterThan(1);

        var responses = items.Select(item => Db.Entry(item).GetExecuteStatementResponse()).ToList();

        responses.Should().AllSatisfy(r => r.Should().NotBeNull());

        var first = responses[0];
        responses.Should().AllSatisfy(r => r.Should().BeSameAs(first));
    }

    [Fact]
    public async Task ToListAsync_WithLimit_PopulatesResponseOnEntities()
    {
        var items = await Db.SimpleItems.Limit(2).ToListAsync(CancellationToken);
        items.Should().HaveCount(2);

        foreach (var item in items)
        {
            var response = Db.Entry(item).GetExecuteStatementResponse();
            response.Should().NotBeNull();
            response!.ResponseMetadata.RequestId.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task NoTracking_GetExecuteStatementResponse_ReturnsNull()
    {
        var items = await Db.SimpleItems.AsNoTracking().ToListAsync(CancellationToken);
        items.Should().NotBeEmpty();

        foreach (var item in items)
        {
            var response = Db.Entry(item).GetExecuteStatementResponse();
            response.Should().BeNull("per-entity response access requires a tracking query");
        }
    }

    [Fact]
    public async Task SuccessiveQueries_EachEntityCarriesItsOwnResponse()
    {
        var first = await Db.SimpleItems.Where(x => x.Pk == "ITEM#1").FirstAsync(CancellationToken);

        var firstResponse = Db.Entry(first).GetExecuteStatementResponse();
        firstResponse.Should().NotBeNull();

        var second =
            await Db.SimpleItems.Where(x => x.Pk == "ITEM#2").FirstAsync(CancellationToken);

        var secondResponse = Db.Entry(second).GetExecuteStatementResponse();
        secondResponse.Should().NotBeNull();

        firstResponse!
            .ResponseMetadata
            .RequestId
            .Should()
            .NotBe(secondResponse!.ResponseMetadata.RequestId);

        Db.Entry(first).GetExecuteStatementResponse().Should().BeSameAs(firstResponse);
    }
}
