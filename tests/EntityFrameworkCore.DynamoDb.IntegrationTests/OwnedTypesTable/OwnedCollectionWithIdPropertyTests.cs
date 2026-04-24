using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>
///     Regression tests for the scenario where an owned collection element has a CLR property
///     named <c>Id</c> with an explicit <c>OwnsMany</c> lambda configuration. Prior to the fix in
///     <c>DynamoKeyDiscoveryConvention</c>, EF Core's <c>Id</c>-based key discovery would set the
///     <c>Id</c> property as the primary key at explicit configuration source, blocking
///     <c>OwnedTypePrimaryKeyConvention</c> from adding the required <c>__OwnedOrdinal</c> shadow
///     key property and causing <c>InvalidOperationException</c> at model build time.
/// </summary>
public class OwnedCollectionWithIdPropertyTests(DynamoContainerFixture fixture)
    : OwnedCollectionWithIdPropertyTestFixture(fixture)
{
    [Fact]
    public void ModelBuilds_WithoutException_WhenOwnedCollectionElementHasIdProperty()
    {
        var model = Db.Model;
        model.Should().NotBeNull();

        var reportType = model.FindEntityType(typeof(AnalysisReport));
        reportType.Should().NotBeNull();

        var resultType = model.GetEntityTypes()
            .Single(e => e.ClrType == typeof(ScoredResult));
        resultType.Should().NotBeNull();

        var ordinalProperty = resultType.GetProperties()
            .SingleOrDefault(p => p.IsOwnedOrdinalKeyProperty());
        ordinalProperty.Should().NotBeNull("owned collection element must have an ordinal shadow key");

        var pk = resultType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Should().Contain(ordinalProperty);
    }

    [Fact]
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
        var loaded = await readCtx.Reports
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

    [Fact]
    public async Task OwnedCollectionElements_WithIdProperty_HaveOrdinalKeysAssigned()
    {
        var report = new AnalysisReport
        {
            Pk = "ANALYSIS#ORD1",
            Results =
            [
                new ScoredResult { Id = "question#111", Score = 0.8f },
                new ScoredResult { Id = "question#222", Score = 0.7f },
                new ScoredResult { Id = "question#333", Score = 0.6f },
            ],
        };

        await Db.AddAsync(report, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);

        using var readCtx = OwnedCollectionWithIdPropertyDbContext.Create(Client);
        var loaded = await readCtx.Reports
            .Where(r => r.Pk == "ANALYSIS#ORD1")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        readCtx.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.TrackAll);

        loaded.Results.Should().HaveCount(3);

        for (var i = 0; i < loaded.Results.Count; i++)
        {
            var entry = readCtx.Entry(loaded.Results[i]);
            entry.State.Should().NotBe(EntityState.Detached);

            var ordinalProperty =
                entry.Metadata.GetProperties().Single(p => p.IsOwnedOrdinalKeyProperty());

            entry.Metadata.FindPrimaryKey()!.Properties.Should().Contain(ordinalProperty);
            entry.Property(ordinalProperty.Name).CurrentValue.Should().Be(i + 1);
        }
    }

    [Fact]
    public async Task SaveAndLoad_AnalysisReport_WithEmptyResults_RoundTrips()
    {
        var report = new AnalysisReport
        {
            Pk = "ANALYSIS#EMPTY1",
            Results = [],
        };

        await Db.AddAsync(report, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);

        using var readCtx = OwnedCollectionWithIdPropertyDbContext.Create(Client);
        var loaded = await readCtx.Reports
            .Where(r => r.Pk == "ANALYSIS#EMPTY1")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        loaded.Pk.Should().Be("ANALYSIS#EMPTY1");
        loaded.Results.Should().BeEmpty();
    }
}