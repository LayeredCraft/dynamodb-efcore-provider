using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.FunkyDataModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public abstract class FunkyDataQueryDynamoTest
    : FunkyDataQueryTestBase<FunkyDataQueryDynamoTest.FunkyDataQueryDynamoFixture>
{
    protected FunkyDataQueryDynamoTest(FunkyDataQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(FunkyDataQueryDynamoTest));

    public override Task String_contains_on_argument_with_wildcard_constant(bool async)
        => NoSyncTest(async, base.String_contains_on_argument_with_wildcard_constant);

    public override Task String_contains_on_argument_with_wildcard_parameter(bool async)
        => NoSyncTest(async, base.String_contains_on_argument_with_wildcard_parameter);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_contains_on_argument_with_wildcard_column(bool async)
        => base.String_contains_on_argument_with_wildcard_column(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_contains_on_argument_with_wildcard_column_negated(bool async)
        => base.String_contains_on_argument_with_wildcard_column_negated(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task String_starts_with_on_argument_with_wildcard_constant(bool async)
        => base.String_starts_with_on_argument_with_wildcard_constant(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task String_starts_with_on_argument_with_wildcard_parameter(bool async)
        => base.String_starts_with_on_argument_with_wildcard_parameter(async);

    public override Task String_starts_with_on_argument_with_bracket(bool async)
        => NoSyncTest(async, base.String_starts_with_on_argument_with_bracket);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_starts_with_on_argument_with_wildcard_column(bool async)
        => base.String_starts_with_on_argument_with_wildcard_column(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_starts_with_on_argument_with_wildcard_column_negated(bool async)
        => base.String_starts_with_on_argument_with_wildcard_column_negated(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task String_ends_with_on_argument_with_wildcard_constant(bool async)
        => base.String_ends_with_on_argument_with_wildcard_constant(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task String_ends_with_on_argument_with_wildcard_parameter(bool async)
        => base.String_ends_with_on_argument_with_wildcard_parameter(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_on_argument_with_wildcard_column(bool async)
        => base.String_ends_with_on_argument_with_wildcard_column(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_on_argument_with_wildcard_column_negated(bool async)
        => base.String_ends_with_on_argument_with_wildcard_column_negated(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_inside_conditional(bool async)
        => base.String_ends_with_inside_conditional(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_inside_conditional_negated(bool async)
        => base.String_ends_with_inside_conditional_negated(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_equals_nullable_column(bool async)
        => base.String_ends_with_equals_nullable_column(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task String_ends_with_not_equals_nullable_column(bool async)
        => base.String_ends_with_not_equals_nullable_column(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task String_FirstOrDefault_and_LastOrDefault(bool async)
        => base.String_FirstOrDefault_and_LastOrDefault(async);

    public override Task String_Contains_and_StartsWith_with_same_parameter(bool async)
        => NoSyncTest(async, base.String_Contains_and_StartsWith_with_same_parameter);

    protected override void ClearLog() => Fixture.ClearSql();

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    public class FunkyDataQueryDynamoFixture
        : FunkyDataQueryFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.ManyServiceProvidersCreatedWarning,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            => modelBuilder.Entity<FunkyCustomer>(entity =>
            {
                entity.ToTable("FunkyCustomers").HasPartitionKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
            });
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class FunkyDataQueryDynamoTestDefault : FunkyDataQueryDynamoTest
    {
        public FunkyDataQueryDynamoTestDefault(
            FunkyDataQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
