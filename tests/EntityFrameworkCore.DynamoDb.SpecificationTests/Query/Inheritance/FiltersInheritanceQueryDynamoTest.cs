using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
#if NET10_0
using Microsoft.EntityFrameworkCore.Query;
#else
using Microsoft.EntityFrameworkCore.Query.Inheritance;
#endif

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Inheritance;

/// <summary>Filtered inheritance query specification tests for the DynamoDB provider.</summary>
public abstract class FiltersInheritanceQueryDynamoTest
    : FiltersInheritanceQueryTestBase<
        FiltersInheritanceQueryDynamoTest.FiltersInheritanceQueryDynamoFixture>
{
    protected FiltersInheritanceQueryDynamoTest(FiltersInheritanceQueryDynamoFixture fixture) :
        base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(FiltersInheritanceQueryDynamoTest));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_animal(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_animal(a));

    public override Task Can_use_is_kiwi(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_is_kiwi(a));

    public override Task Can_use_is_kiwi_with_other_predicate(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Can_use_is_kiwi_with_other_predicate(a));

    public override Task Can_use_is_kiwi_in_projection(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_is_kiwi_in_projection(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_bird(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird_predicate(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Can_use_of_type_bird_predicate(a));

    public override Task Can_use_of_type_bird_with_projection(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Can_use_of_type_bird_with_projection(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird_first(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_bird_first(a));

    public override Task Can_use_of_type_kiwi(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_kiwi(a));

    public override Task Can_use_derived_set(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_derived_set(a));

    [ConditionalTheory(Skip = SkipReason.SyncQueriesNotSupported)]
    public override Task Can_use_IgnoreQueryFilters_and_GetDatabaseValues(bool async)
        => base.Can_use_IgnoreQueryFilters_and_GetDatabaseValues(async);

    public class FiltersInheritanceQueryDynamoFixture
        : Query.InheritanceQueryDynamoTest.InheritanceQueryDynamoFixture
    {
        public override bool EnableFilters => true;
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class FiltersInheritanceQueryDynamoTestDefault : FiltersInheritanceQueryDynamoTest
    {
        public FiltersInheritanceQueryDynamoTestDefault(
            FiltersInheritanceQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
