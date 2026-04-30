using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>
///     Regression tests for the scenario where a complex collection element has a CLR property
///     named <c>Id</c> with an explicit <c>ComplexCollection</c> lambda configuration. Prior to
///     the fix in <c>DynamoKeyDiscoveryConvention</c>, EF Core's <c>Id</c>-based key discovery
///     would interfere with complex type element configuration.
/// </summary>
public class OwnedCollectionWithIdPropertyTests(DynamoContainerFixture fixture)
    : OwnedCollectionWithIdPropertyTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ModelBuilds_WithoutException_WhenComplexCollectionElementHasIdProperty()
    {
        var model = Db.Model;
        model.Should().NotBeNull();

        var reportType = model.FindEntityType(typeof(AnalysisReport));
        reportType.Should().NotBeNull();

        // ScoredResult is a complex type — not an entity type.
        var complexCollection =
            reportType!.GetComplexProperties().SingleOrDefault(cp => cp.Name == "Results");
        complexCollection
            .Should()
            .NotBeNull("AnalysisReport must have a complex collection Results");
        complexCollection!.IsCollection.Should().BeTrue();
        complexCollection.ComplexType.ClrType.Should().Be(typeof(ScoredResult));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveAndLoad_AnalysisReport_RoundTrips_WithIdAttributeNameMapping()
    {
        var report = new AnalysisReport
        {
            Pk = "ANALYSIS#RT1",
            Results =
            [
                new ScoredResult { Id = "question#aaa", Score = 0.9512f },
                new ScoredResult { Id = "question#bbb", Score = 0.7431f },
            ],
        };

        await Db.AddAsync(report, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);

        using var readCtx = OwnedCollectionWithIdPropertyDbContext.Create(Client);
        var loaded =
            await readCtx
                .Reports
                .Where(r => r.Pk == "ANALYSIS#RT1")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        loaded.Pk.Should().Be("ANALYSIS#RT1");
        loaded.Results.Should().HaveCount(2);
        loaded.Results[0].Id.Should().Be("question#aaa");
        loaded.Results[0].Score.Should().BeApproximately(0.9512f, 0.0001f);
        loaded.Results[1].Id.Should().Be("question#bbb");
        loaded.Results[1].Score.Should().BeApproximately(0.7431f, 0.0001f);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveAndLoad_AnalysisReport_WithEmptyResults_RoundTrips()
    {
        var report = new AnalysisReport { Pk = "ANALYSIS#EMPTY1", Results = [], };

        await Db.AddAsync(report, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);

        using var readCtx = OwnedCollectionWithIdPropertyDbContext.Create(Client);
        var loaded =
            await readCtx
                .Reports
                .Where(r => r.Pk == "ANALYSIS#EMPTY1")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        loaded.Pk.Should().Be("ANALYSIS#EMPTY1");
        loaded.Results.Should().BeEmpty();
    }
}
