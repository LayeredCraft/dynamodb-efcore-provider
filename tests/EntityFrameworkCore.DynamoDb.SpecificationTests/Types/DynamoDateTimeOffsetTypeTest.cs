using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Types;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoDateTimeOffsetTypeTest(
    DynamoDateTimeOffsetTypeTest.DateTimeOffsetTypeFixture fixture,
    DynamoSpecificationContainerFixture containerFixture)
    : TypeTestBase<DateTimeOffset, DynamoDateTimeOffsetTypeTest.DateTimeOffsetTypeFixture>(fixture)
{
    private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(DynamoDateTimeOffsetTypeTest));

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

    public class DateTimeOffsetTypeFixture
        : TypeFixtureBase<DateTimeOffset>, IDynamoSpecificationFixture
    {
        public override DateTimeOffset Value { get; } = new(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);

        public override DateTimeOffset OtherValue { get; } =
            new(2021, 2, 3, 4, 5, 6, TimeSpan.Zero);

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

            modelBuilder
                .Entity<TypeEntity<DateTimeOffset>>()
                .ToTable("DateTimeOffsetTypes")
                .HasPartitionKey(e => e.Id);
        }
    }
}
