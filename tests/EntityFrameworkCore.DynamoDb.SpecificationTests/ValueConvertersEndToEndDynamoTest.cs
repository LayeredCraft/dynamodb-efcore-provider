using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class ValueConvertersEndToEndDynamoTest(
    ValueConvertersEndToEndDynamoTest.ValueConvertersEndToEndDynamoFixture fixture)
    : ValueConvertersEndToEndTestBase<
        ValueConvertersEndToEndDynamoTest.ValueConvertersEndToEndDynamoFixture>(fixture)
{
    public override Task Can_insert_and_read_back_with_conversions(int[] valueOrder)
        => base.Can_insert_and_read_back_with_conversions(valueOrder);

    public class ValueConvertersEndToEndDynamoFixture
        : ValueConvertersEndToEndFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder
                .Entity<ConvertingEntity>()
                .ToTable("ConvertingEntities")
                .HasPartitionKey(e => e.Id);
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class ValueConvertersEndToEndDynamoTestDefault : ValueConvertersEndToEndDynamoTest
    {
        public ValueConvertersEndToEndDynamoTestDefault(
            ValueConvertersEndToEndDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
