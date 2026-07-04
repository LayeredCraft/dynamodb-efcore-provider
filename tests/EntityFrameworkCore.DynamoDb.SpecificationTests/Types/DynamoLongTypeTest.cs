using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Types;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoLongTypeTest(
    DynamoLongTypeTest.LongTypeFixture fixture,
    DynamoSpecificationContainerFixture containerFixture)
    : TypeTestBase<long, DynamoLongTypeTest.LongTypeFixture>(fixture)
{
    private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;

#if NET11_0_OR_GREATER
    public override async Task Equality_in_query_with_parameter()
    {
        await using var context = Fixture.CreateContext();

        var results = await context.Set<TypeEntity<long>>()
            .Where(e => e.Value.Equals(Fixture.Value))
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    public override async Task Equality_in_query_with_constant()
    {
        await using var context = Fixture.CreateContext();

        var entityParameter = Expression.Parameter(typeof(TypeEntity<long>), "e");
        var predicate =
            Expression.Lambda<Func<TypeEntity<long>, bool>>(
                Expression.Equal(
                    Expression.Property(entityParameter, nameof(TypeEntity<long>.Value)),
                    Expression.Constant(Fixture.Value)),
                entityParameter);

        var results = await context.Set<TypeEntity<long>>()
            .Where(predicate)
            .ToListAsync();
        var result = results.Single();

        Assert.Equal(Fixture.Value, result.Value, Fixture.Comparer);
    }

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Primitive_collection_in_query() => base.Primitive_collection_in_query();
#else
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Equality_in_query() => base.Equality_in_query();
#endif

    public class LongTypeFixture : TypeFixtureBase<long>, IDynamoSpecificationFixture
    {
        public override long Value { get; } = long.MinValue;
        public override long OtherValue { get; } = long.MaxValue;

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

            modelBuilder.Entity<TypeEntity<long>>().ToTable("LongTypes").HasPartitionKey(e => e.Id);
        }
    }
}
