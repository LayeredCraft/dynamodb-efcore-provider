#if NET11_0_OR_GREATER
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Inheritance;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Inheritance;

/// <summary>Inheritance complex-type query specification tests for the DynamoDB provider.</summary>
public abstract class InheritanceComplexTypesQueryDynamoTest
    : InheritanceComplexTypesQueryTestBase<
        InheritanceComplexTypesQueryDynamoTest.InheritanceComplexTypesQueryDynamoFixture>
{
    protected InheritanceComplexTypesQueryDynamoTest(InheritanceComplexTypesQueryDynamoFixture fixture)
        : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(InheritanceComplexTypesQueryDynamoTest));

    public override Task Filter_on_complex_type_property_on_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Filter_on_complex_type_property_on_derived_type(a));

    public override Task Filter_on_complex_type_property_on_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Filter_on_complex_type_property_on_base_type(a));

    public override Task Filter_on_nested_complex_type_property_on_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Filter_on_nested_complex_type_property_on_derived_type(a));

    public override Task Filter_on_nested_complex_type_property_on_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Filter_on_nested_complex_type_property_on_base_type(a));

    public override Task Project_complex_type_on_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Project_complex_type_on_derived_type(a));

    public override Task Project_complex_type_on_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Project_complex_type_on_base_type(a));

    public override Task Project_nested_complex_type_on_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Project_nested_complex_type_on_derived_type(a));

    public override Task Project_nested_complex_type_on_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Project_nested_complex_type_on_base_type(a));

    [ConditionalTheory(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Subquery_over_complex_collection(bool async)
        => base.Subquery_over_complex_collection(async);

    public class InheritanceComplexTypesQueryDynamoFixture
        : Query.InheritanceQueryDynamoTest.InheritanceQueryDynamoFixture;

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class InheritanceComplexTypesQueryDynamoTestDefault
        : InheritanceComplexTypesQueryDynamoTest
    {
        public InheritanceComplexTypesQueryDynamoTestDefault(
            InheritanceComplexTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
#endif
