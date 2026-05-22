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
            => await c.Products.Where(p => p.Id == 1).AsUnsafeFilteredQuery().FirstAsync());
    }

    [ConditionalTheory(Skip = "DynamoDB does not support Last queries.")]
    public override Task Last(bool async) => Task.CompletedTask;

    public override async Task SaveChanges(bool async)
    {
        if (!async)
            return;

        await ConcurrencyDetectorTest(async c =>
        {
            c.Products.Add(new Product { Id = 3, Name = "Unicorn Horseshoe Protection Pack" });
            return await c.SaveChangesAsync();
        });

        await using var verificationContext = CreateContext();
        var newProduct = await verificationContext.Products.FirstOrDefaultAsync(p => p.Id == 3);
        Assert.NotNull(newProduct);
        verificationContext.Products.Remove(newProduct);
        await verificationContext.SaveChangesAsync();
    }

    [ConditionalTheory(Skip = "DynamoDB does not support Single queries.")]
    public override Task Single(bool async) => Task.CompletedTask;

    public override Task ToList(bool async)
    {
        if (!async)
            return Task.CompletedTask;

        return ConcurrencyDetectorTest(async c => await c.Products.AllowScan().ToListAsync());
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
