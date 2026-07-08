using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class ValueConvertersEndToEndDynamoTest(
    ValueConvertersEndToEndDynamoTest.ValueConvertersEndToEndDynamoFixture fixture)
    : ValueConvertersEndToEndTestBase<
        ValueConvertersEndToEndDynamoTest.ValueConvertersEndToEndDynamoFixture>(fixture)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
    {
        var testClass = typeof(ValueConvertersEndToEndDynamoTest);
        // ValueConvertersEndToEndTestBase includes protected converter unit facts; only inherited
        // public spec tests are part of this provider override surface.
        var inheritedPublicOverridableTests = testClass
            .GetMethods()
            .Where(method => method.DeclaringType != testClass
                && method.IsPublic
                && method.IsVirtual
                && !method.IsFinal
                && (Attribute.IsDefined(method, typeof(ConditionalFactAttribute))
                    || Attribute.IsDefined(method, typeof(ConditionalTheoryAttribute))))
            .Select(method => method.Name);

        Assert.Empty(inheritedPublicOverridableTests);
    }

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
