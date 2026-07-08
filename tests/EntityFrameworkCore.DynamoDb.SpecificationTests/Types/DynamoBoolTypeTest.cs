using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Types;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoBoolTypeTest(
    DynamoBoolTypeTest.BoolTypeFixture fixture,
    DynamoSpecificationContainerFixture containerFixture)
    : TypeTestBase<bool, DynamoBoolTypeTest.BoolTypeFixture>(fixture)
{
    private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(DynamoBoolTypeTest));

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Equality_in_query_with_parameter()
        => base.Equality_in_query_with_parameter();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Equality_in_query_with_constant()
        => base.Equality_in_query_with_constant();

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Primitive_collection_in_query() => base.Primitive_collection_in_query();

    public override Task SaveChanges() => base.SaveChanges();
#else
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Equality_in_query() => base.Equality_in_query();
#endif

    public class BoolTypeFixture : TypeFixtureBase<bool>, IDynamoSpecificationFixture
    {
        public override bool Value { get; } = true;
        public override bool OtherValue { get; } = false;

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<TypeEntity<bool>>().ToTable("BoolTypes").HasPartitionKey(e => e.Id);
        }
    }
}
