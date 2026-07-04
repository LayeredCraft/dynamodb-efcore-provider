using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class ConcurrencyDetectorDisabledDynamoTest(
    ConcurrencyDetectorDisabledDynamoTest.ConcurrencyDetectorDisabledDynamoFixture fixture)
    : ConcurrencyDetectorDisabledTestBase<ConcurrencyDetectorDisabledDynamoTest.
        ConcurrencyDetectorDisabledDynamoFixture>(fixture)
{
    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ConcurrencyDetectorDisabledDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Any(bool async) => base.Any(async);

    [ConditionalTheory(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Count(bool async) => base.Count(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task Find(bool async) => base.Find(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task First(bool async) => base.First(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last(bool async) => base.Last(async);

    [ConditionalTheory(Skip = SkipReason.SyncSaveChangesNotSupported)]
    public override Task SaveChanges(bool async) => base.SaveChanges(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task Single(bool async) => base.Single(async);

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task ToList(bool async) => base.ToList(async);

    /// <summary>Fixture for DynamoDB concurrency detector disabled tests.</summary>
    public class ConcurrencyDetectorDisabledDynamoFixture : ConcurrencyDetectorFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client))
                .EnableThreadSafetyChecks(false);
    }
}
