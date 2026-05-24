using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class WithConstructorsDynamoTest(
    WithConstructorsDynamoTest.WithConstructorsDynamoFixture fixture)
    : WithConstructorsTestBase<WithConstructorsDynamoTest.WithConstructorsDynamoFixture>(fixture)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(WithConstructorsDynamoTest));

    public override Task Query_and_update_using_constructors_with_property_parameters()
        => base.Query_and_update_using_constructors_with_property_parameters();

    public override void Query_with_keyless_type() => base.Query_with_keyless_type();

    public override void Query_with_context_injected() => base.Query_with_context_injected();

    public override void Query_with_context_injected_into_property()
        => base.Query_with_context_injected_into_property();

    public override void Query_with_context_injected_into_constructor_with_property()
        => base.Query_with_context_injected_into_constructor_with_property();

    public override void Attaching_entity_sets_context() => base.Attaching_entity_sets_context();

    public override void Query_with_EntityType_injected() => base.Query_with_EntityType_injected();

    public override void Query_with_EntityType_injected_into_property()
        => base.Query_with_EntityType_injected_into_property();

    public override void Query_with_EntityType_injected_into_constructor_with_property()
        => base.Query_with_EntityType_injected_into_constructor_with_property();

    public override void Attaching_entity_sets_EntityType()
        => base.Attaching_entity_sets_EntityType();

    public override void Query_with_StateManager_injected()
        => base.Query_with_StateManager_injected();

    public override void Query_with_StateManager_injected_into_property()
        => base.Query_with_StateManager_injected_into_property();

    public override void Query_with_StateManager_injected_into_constructor_with_property()
        => base.Query_with_StateManager_injected_into_constructor_with_property();

    public override void Attaching_entity_sets_StateManager()
        => base.Attaching_entity_sets_StateManager();

    public override void Query_with_loader_injected_for_reference()
        => base.Query_with_loader_injected_for_reference();

    public override void Query_with_loader_injected_for_collections()
        => base.Query_with_loader_injected_for_collections();

    public override Task Query_with_loader_injected_for_reference_async()
        => base.Query_with_loader_injected_for_reference_async();

    public override Task Query_with_loader_injected_for_collections_async()
        => base.Query_with_loader_injected_for_collections_async();

    public override void Query_with_POCO_loader_injected_for_reference()
        => base.Query_with_POCO_loader_injected_for_reference();

    public override void Query_with_POCO_loader_injected_for_collections()
        => base.Query_with_POCO_loader_injected_for_collections();

    public override Task Query_with_loader_delegate_injected_for_reference_async()
        => base.Query_with_loader_delegate_injected_for_reference_async();

    public override Task Query_with_loader_delegate_injected_for_collections_async()
        => base.Query_with_loader_delegate_injected_for_collections_async();

    public override void Query_with_loader_injected_into_property_for_reference()
        => base.Query_with_loader_injected_into_property_for_reference();

    public override void Query_with_loader_injected_into_property_for_collections()
        => base.Query_with_loader_injected_into_property_for_collections();

    public override void Attaching_entity_sets_lazy_loader()
        => base.Attaching_entity_sets_lazy_loader();

    public override void Detaching_entity_resets_lazy_loader_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_so_it_can_be_reattached();

    public override void Query_with_loader_injected_into_field_for_reference()
        => base.Query_with_loader_injected_into_field_for_reference();

    public override void Query_with_loader_injected_into_field_for_collections()
        => base.Query_with_loader_injected_into_field_for_collections();

    public override void Attaching_entity_sets_lazy_loader_field()
        => base.Attaching_entity_sets_lazy_loader_field();

    public override void Detaching_entity_resets_lazy_loader_field_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_field_so_it_can_be_reattached();

    public override void Attaching_entity_sets_lazy_loader_delegate()
        => base.Attaching_entity_sets_lazy_loader_delegate();

    public override void Detaching_entity_resets_lazy_loader_delegate_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_delegate_so_it_can_be_reattached();

    public override void Query_with_loader_delegate_injected_into_property_for_reference()
        => base.Query_with_loader_delegate_injected_into_property_for_reference();

    public override void Query_with_loader_delgate_injected_into_property_for_collections()
        => base.Query_with_loader_delgate_injected_into_property_for_collections();

    public override Task Query_with_loader_delegate_injected_into_property_for_reference_async()
        => base.Query_with_loader_delegate_injected_into_property_for_reference_async();

    public override Task Query_with_loader_delegate_injected_into_property_for_collections_async()
        => base.Query_with_loader_delegate_injected_into_property_for_collections_async();

    public override void Query_with_loader_injected_into_property_via_constructor_for_reference()
        => base.Query_with_loader_injected_into_property_via_constructor_for_reference();

    public override void Query_with_loader_injected_into_property_via_constructor_for_collections()
        => base.Query_with_loader_injected_into_property_via_constructor_for_collections();

    public override void
        Query_with_loader_delegate_injected_into_property_via_constructor_for_reference()
        => base.Query_with_loader_delegate_injected_into_property_via_constructor_for_reference();

    public override void
        Query_with_loader_delegate_injected_into_property_via_constructor_for_collections()
        => base.Query_with_loader_delegate_injected_into_property_via_constructor_for_collections();

    public override Task Add_immutable_record() => base.Add_immutable_record();

    public class WithConstructorsDynamoFixture
        : WithConstructorsFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var entity = modelBuilder.Entity(entityType.ClrType);
                entity.ToTable(entityType.ClrType.Name.Replace('`', '_'));

                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey is { Properties.Count: > 0 })
                    entity.HasPartitionKey(primaryKey.Properties[0].Name);
            }
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public class WithConstructorsDynamoTestDefault(
        WithConstructorsDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : WithConstructorsDynamoTest(fixture)
    {
        private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;
    }
}
