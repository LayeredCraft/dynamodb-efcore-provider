using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class ConcurrencyDetectorEnabledDynamoTest(
    ConcurrencyDetectorEnabledDynamoTest.ConcurrencyDetectorEnabledDynamoFixture fixture)
    : ConcurrencyDetectorEnabledTestBase<
        ConcurrencyDetectorEnabledDynamoTest.ConcurrencyDetectorEnabledDynamoFixture>(fixture)
{
    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ConcurrencyDetectorEnabledDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Any(bool async) => base.Any(async);

    [ConditionalTheory(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Count(bool async) => base.Count(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task Find(bool async) => base.Find(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task First(bool async)
        => ConcurrencyDetectorTest(async c
            => await c.Products.AsUnsafeFilteredQuery().FirstAsync());

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last(bool async) => base.Last(async);

    [ConditionalTheory(Skip = SkipReason.SyncSaveChangesNotSupported)]
    public override Task SaveChanges(bool async) => SaveChangesAsync();

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task Single(bool async) => base.Single(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task ToList(bool async) => base.ToList(async);

    private async Task SaveChangesAsync()
    {
        await ConcurrencyDetectorTest(async c =>
        {
            c.Products.Add(new Product { Id = 2, Name = "Unicorn Replacement Horn Pack" });
            return await c.SaveChangesAsync();
        });

        await using var ctx = CreateContext();
        var newProduct = await ctx.Products.FirstOrDefaultAsync(p => p.Id == 2);
        Assert.Null(newProduct);
    }

    /// <summary>Fixture for DynamoDB concurrency detector tests.</summary>
    public class ConcurrencyDetectorEnabledDynamoFixture : ConcurrencyDetectorFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));
    }
}
