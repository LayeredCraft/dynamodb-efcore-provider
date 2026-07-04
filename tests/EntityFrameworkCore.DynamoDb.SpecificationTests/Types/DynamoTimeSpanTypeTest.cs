using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Types;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoTimeSpanTypeTest(
    DynamoTimeSpanTypeTest.TimeSpanTypeFixture fixture,
    DynamoSpecificationContainerFixture containerFixture)
    : TypeTestBase<TimeSpan, DynamoTimeSpanTypeTest.TimeSpanTypeFixture>(fixture)
{
    private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(DynamoTimeSpanTypeTest));

#if NET11_0_OR_GREATER
    public override async Task Equality_in_query_with_parameter()
    {
        await using var context = Fixture.CreateContext();

        var results = await context.Set<TypeEntity<TimeSpan>>()
            .Where(e => e.Value.Equals(Fixture.Value))
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    public override async Task Equality_in_query_with_constant()
    {
        await using var context = Fixture.CreateContext();

        var entityParameter = Expression.Parameter(typeof(TypeEntity<TimeSpan>), "e");
        var predicate =
            Expression.Lambda<Func<TypeEntity<TimeSpan>, bool>>(
                Expression.Equal(
                    Expression.Property(entityParameter, nameof(TypeEntity<TimeSpan>.Value)),
                    Expression.Constant(Fixture.Value)),
                entityParameter);

        var results = await context.Set<TypeEntity<TimeSpan>>()
            .Where(predicate)
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Primitive_collection_in_query() => base.Primitive_collection_in_query();

    public override Task SaveChanges() => base.SaveChanges();
#else
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Equality_in_query() => base.Equality_in_query();
#endif

    public class TimeSpanTypeFixture : TypeFixtureBase<TimeSpan>, IDynamoSpecificationFixture
    {
        public override TimeSpan Value { get; } = new(12, 30, 0);
        public override TimeSpan OtherValue { get; } = new(4, 5, 6);

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
                .Entity<TypeEntity<TimeSpan>>()
                .ToTable("TimeSpanTypes")
                .HasPartitionKey(e => e.Id);
        }
    }
}
