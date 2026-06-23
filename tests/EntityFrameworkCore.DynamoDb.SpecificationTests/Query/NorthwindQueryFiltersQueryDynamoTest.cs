using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind query-filter specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindQueryFiltersQueryDynamoTest
    : NorthwindQueryFiltersQueryTestBase<
        NorthwindQueryDynamoFixture<NorthwindQueryFiltersCustomizer>>
{
    protected NorthwindQueryFiltersQueryDynamoTest(
        NorthwindQueryDynamoFixture<NorthwindQueryFiltersCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindQueryFiltersQueryDynamoTest));

    [ConditionalTheory(Skip = "DynamoDB PartiQL does not support COUNT aggregates.")]
    public override Task Count_query(bool async) => base.Count_query(async);

    public override Task Materialized_query(bool async)
        => NoSyncTest(async, a => base.Materialized_query(a));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Find(bool async) => base.Find(async);

    public override async Task Client_eval(bool async)
    {
        using var context = Fixture.CreateContext();
        var query = context.Set<Product>();

        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>((Action)(() => query.ToList()));

        Assert.Contains("could not be translated", exception.Message);
    }

    public override Task Materialized_query_parameter(bool async)
        => NoSyncTest(async, a => base.Materialized_query_parameter(a));

    public override Task Materialized_query_parameter_new_context(bool async)
        => NoSyncTest(async, a => base.Materialized_query_parameter_new_context(a));

    public override Task Projection_query(bool async)
        => NoSyncTest(async, a => base.Projection_query(a));

    public override Task Projection_query_parameter(bool async)
        => NoSyncTest(async, a => base.Projection_query_parameter(a));

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Include_query(bool async) => base.Include_query(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Include_query_opt_out(bool async) => base.Include_query_opt_out(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Included_many_to_one_query2(bool async)
        => base.Included_many_to_one_query2(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Included_many_to_one_query(bool async)
        => base.Included_many_to_one_query(async);

    [ConditionalTheory(Skip = SkipReason.EntityTypeNotMappedInFixture)]
    public override Task
        Project_reference_that_itself_has_query_filter_with_another_reference(bool async)
        => base.Project_reference_that_itself_has_query_filter_with_another_reference(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Included_one_to_many_query_with_client_eval(bool async)
        => base.Included_one_to_many_query_with_client_eval(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Navs_query(bool async) => base.Navs_query(async);

    public override void Compiled_query()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Compiled_query());

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Entity_Equality(bool async) => base.Entity_Equality(async);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindQueryFiltersQueryDynamoTestDefault
        : NorthwindQueryFiltersQueryDynamoTest
    {
        public NorthwindQueryFiltersQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NorthwindQueryFiltersCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
