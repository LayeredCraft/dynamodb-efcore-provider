using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>
///     Integration tests for per-entity
///     <see cref="Amazon.DynamoDBv2.Model.ExecuteStatementResponse" /> access via
///     <c>context.Entry(entity).GetExecuteStatementResponse()</c>.
/// </summary>
public class ExecuteStatementResponseTests(SimpleTableDynamoFixture fixture)
    : SimpleTableTestBase(fixture)
{
    // -----------------------------------------------------------------------
    // Single-page queries (FirstAsync, Limit(n))
    // -----------------------------------------------------------------------

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
        // All 4 seed items fit on a single page — every entity should reference the same
        // ExecuteStatementResponse object.
        var items = await Db.SimpleItems.ToListAsync(CancellationToken);
        items.Should().HaveCountGreaterThan(1);

        var responses = items.Select(item => Db.Entry(item).GetExecuteStatementResponse()).ToList();

        responses.Should().AllSatisfy(r => r.Should().NotBeNull());

        // All entities from the same page share the exact same object reference.
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

    // -----------------------------------------------------------------------
    // No-tracking queries
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoTracking_GetExecuteStatementResponse_ReturnsNull()
    {
        // For no-tracking queries the entity is never materialized into a tracked entry,
        // so the shadow property value is not populated — GetExecuteStatementResponse returns null.
        var items = await Db.SimpleItems.AsNoTracking().ToListAsync(CancellationToken);
        items.Should().NotBeEmpty();

        foreach (var item in items)
        {
            // Calling Db.Entry() on a detached entity does not throw, but the shadow
            // property value was never written since no InternalEntityEntry tracked it.
            var response = Db.Entry(item).GetExecuteStatementResponse();
            response.Should().BeNull("per-entity response access requires a tracking query");
        }
    }

    // -----------------------------------------------------------------------
    // Successive queries — each entity carries the response from its own query
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SuccessiveQueries_EachEntityCarriesItsOwnResponse()
    {
        // First query
        var first = await Db.SimpleItems.Where(x => x.Pk == "ITEM#1").FirstAsync(CancellationToken);

        var firstResponse = Db.Entry(first).GetExecuteStatementResponse();
        firstResponse.Should().NotBeNull();

        // Second query for a different entity
        var second =
            await Db.SimpleItems.Where(x => x.Pk == "ITEM#2").FirstAsync(CancellationToken);

        var secondResponse = Db.Entry(second).GetExecuteStatementResponse();
        secondResponse.Should().NotBeNull();

        // The two responses are distinct objects (different requests, different request IDs).
        firstResponse!
            .ResponseMetadata
            .RequestId
            .Should()
            .NotBe(secondResponse!.ResponseMetadata.RequestId);

        // The first entity's shadow property is not mutated by the second query.
        Db.Entry(first).GetExecuteStatementResponse().Should().BeSameAs(firstResponse);
    }
}
