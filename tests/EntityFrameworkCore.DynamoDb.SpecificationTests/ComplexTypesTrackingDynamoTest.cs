using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Complex type tracking specification tests for the DynamoDB provider.</summary>
[Collection(DynamoSpecificationCollection.Name)]
public class ComplexTypesTrackingDynamoTest
    : ComplexTypesTrackingTestBase<ComplexTypesTrackingDynamoTest.ComplexTypesTrackingDynamoFixture>
{
    /// <summary>Creates complex type tracking specification tests.</summary>
    public ComplexTypesTrackingDynamoTest(
        ComplexTypesTrackingDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : base(fixture)
        => _ = containerFixture;

    /// <summary>Ensures all inherited specification tests are reviewed by this provider.</summary>
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(ComplexTypesTrackingDynamoTest));

    /// <inheritdoc />
    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<DbContext, Task> testOperation,
        Func<DbContext, Task>? nestedTestOperation1 = null,
        Func<DbContext, Task>? nestedTestOperation2 = null,
        Func<DbContext, Task>? nestedTestOperation3 = null)
    {
        await using var context = CreateContext();
        try
        {
            await context
                .Database
                .CreateExecutionStrategy()
                .ExecuteAsync(
                    context,
                    async _ =>
                    {
                        await using (var innerContext = CreateContext())
                            await testOperation(innerContext).ConfigureAwait(false);

                        if (nestedTestOperation1 is null)
                            return;

                        await using (var innerContext = CreateContext())
                            await nestedTestOperation1(innerContext).ConfigureAwait(false);

                        if (nestedTestOperation2 is null)
                            return;

                        await using (var innerContext = CreateContext())
                            await nestedTestOperation2(innerContext).ConfigureAwait(false);

                        if (nestedTestOperation3 is null)
                            return;

                        await using (var innerContext = CreateContext())
                            await nestedTestOperation3(innerContext).ConfigureAwait(false);
                    });
        }
        finally
        {
            await Fixture.TestStore.CleanAsync(context).ConfigureAwait(false);
        }
    }

    /// <summary>Fixture for DynamoDB complex type tracking specification tests.</summary>
    public class ComplexTypesTrackingDynamoFixture : FixtureBase, IDynamoSpecificationFixture
    {
        /// <inheritdoc />
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        /// <inheritdoc />
        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.ManyServiceProvidersCreatedWarning,
                    DynamoEventId.NoCompatibleSecondaryIndexFound,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        /// <inheritdoc />
        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }
    }
}
