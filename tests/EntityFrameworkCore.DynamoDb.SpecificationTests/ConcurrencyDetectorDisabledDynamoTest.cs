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

    public override Task Find(bool async)
    {
        if (!async)
        {
            AssertSyncQueryUnsupported(context => context.Products.Find(1));
            return Task.CompletedTask;
        }

        return base.Find(async);
    }

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task First(bool async) => base.First(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last(bool async) => base.Last(async);

    public override Task SaveChanges(bool async)
    {
        if (!async)
        {
            AssertSyncSaveChangesUnsupported(3, "Unicorn Horseshoe Protection Pack");
            return Task.CompletedTask;
        }

        return base.SaveChanges(async);
    }

    public override Task Single(bool async)
    {
        if (!async)
        {
            AssertSyncQueryUnsupported(context => context.Products.Single(p => p.Id == 1));
            return Task.CompletedTask;
        }

        return base.Single(async);
    }

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task ToList(bool async) => base.ToList(async);

    private void AssertSyncQueryUnsupported(Action<ConcurrencyDetectorDbContext> testCode)
    {
        using var context = CreateContext();
        DynamoTestHelpers.Instance.NoSyncTest(() => testCode(context));
    }

    private void AssertSyncSaveChangesUnsupported(int id, string name)
    {
        using var context = CreateContext();
        context.Products.Add(new Product { Id = id, Name = name });

        var exception = Assert.Throws<NotSupportedException>(() => context.SaveChanges());
        Assert.Contains("synchronous SaveChanges", exception.Message);
    }

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
