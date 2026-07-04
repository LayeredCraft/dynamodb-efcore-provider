using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Types;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoTimeOnlyTypeTest(
    DynamoTimeOnlyTypeTest.TimeOnlyTypeFixture fixture,
    DynamoSpecificationContainerFixture containerFixture)
    : TypeTestBase<TimeOnly, DynamoTimeOnlyTypeTest.TimeOnlyTypeFixture>(fixture)
{
    private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;

#if NET11_0_OR_GREATER
    public override async Task Equality_in_query_with_parameter()
    {
        await using var context = Fixture.CreateContext();

        var results = await context.Set<TypeEntity<TimeOnly>>()
            .Where(e => e.Value.Equals(Fixture.Value))
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    public override async Task Equality_in_query_with_constant()
    {
        await using var context = Fixture.CreateContext();

        var entityParameter = Expression.Parameter(typeof(TypeEntity<TimeOnly>), "e");
        var predicate =
            Expression.Lambda<Func<TypeEntity<TimeOnly>, bool>>(
                Expression.Equal(
                    Expression.Property(entityParameter, nameof(TypeEntity<TimeOnly>.Value)),
                    Expression.Constant(Fixture.Value)),
                entityParameter);

        var results = await context.Set<TypeEntity<TimeOnly>>()
            .Where(predicate)
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Primitive_collection_in_query() => base.Primitive_collection_in_query();
#else
    public override async Task Equality_in_query()
    {
        await using var context = Fixture.CreateContext();

        var results =
            await context
                .Set<TypeEntity<TimeOnly>>()
                .Where(e => e.Value.Equals(Fixture.Value))
                .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }
#endif

    public class TimeOnlyTypeFixture : TypeFixtureBase<TimeOnly>, IDynamoSpecificationFixture
    {
        public override TimeOnly Value { get; } = new(12, 30, 0);
        public override TimeOnly OtherValue { get; } = new(4, 5, 6);

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
                .Entity<TypeEntity<TimeOnly>>()
                .ToTable("TimeOnlyTypes")
                .HasPartitionKey(e => e.Id);
        }
    }
}
