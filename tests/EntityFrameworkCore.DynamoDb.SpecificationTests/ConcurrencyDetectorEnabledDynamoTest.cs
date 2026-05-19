using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

#nullable disable

/// <summary>Concurrency detector specification tests for the DynamoDB provider.</summary>
[Collection(DynamoSpecificationCollection.Name)]
public sealed class ConcurrencyDetectorEnabledDynamoTest(
    ConcurrencyDetectorEnabledDynamoTest.ConcurrencyDetectorEnabledDynamoFixture fixture)
    : ConcurrencyDetectorEnabledTestBase<
        ConcurrencyDetectorEnabledDynamoTest.ConcurrencyDetectorEnabledDynamoFixture>(fixture)
{
    /// <summary>Ensures all inherited specification tests are reviewed by this provider.</summary>
    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ConcurrencyDetectorEnabledDynamoTest));

    [ConditionalTheory(Skip = "DynamoDB does not support Any queries.")]
    public override Task Any(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = "DynamoDB does not support Count queries.")]
    public override Task Count(bool async) => Task.CompletedTask;

    public override Task Find(bool async) => async ? base.Find(async) : Task.CompletedTask;

    public override Task First(bool async)
    {
        if (!async)
            return Task.CompletedTask;

        return ConcurrencyDetectorTest(async c
            => await c.Products.AsUnsafeFilteredQuery().FirstAsync());
    }

    [ConditionalTheory(Skip = "DynamoDB does not support Last queries.")]
    public override Task Last(bool async) => Task.CompletedTask;

    public override Task SaveChanges(bool async)
        => async ? base.SaveChanges(async) : Task.CompletedTask;

    [ConditionalTheory(Skip = "DynamoDB does not support Single queries.")]
    public override Task Single(bool async) => Task.CompletedTask;

    public override Task ToList(bool async) => async ? base.ToList(async) : Task.CompletedTask;

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
