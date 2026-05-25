using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class WithConstructorsDynamoTest
    : WithConstructorsTestBase<WithConstructorsDynamoTest.WithConstructorsDynamoFixture>
{
    protected WithConstructorsDynamoTest(WithConstructorsDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(WithConstructorsDynamoTest));

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_and_update_using_constructors_with_property_parameters()
        => base.Query_and_update_using_constructors_with_property_parameters();

    [ConditionalFact(Skip = SkipReason.KeylessEntityTypesNotSupported)]
    public override void Query_with_keyless_type() => base.Query_with_keyless_type();

    public override void Query_with_context_injected()
        => RunSynchronously(QueryWithContextInjectedAsync);

    public override void Query_with_context_injected_into_property()
        => RunSynchronously(QueryWithContextInjectedIntoPropertyAsync);

    public override void Query_with_context_injected_into_constructor_with_property()
        => RunSynchronously(QueryWithContextInjectedIntoConstructorWithPropertyAsync);

    public override void Attaching_entity_sets_context()
        => RunSynchronously(AttachingEntitySetsContextAsync);

    public override void Query_with_EntityType_injected()
        => RunSynchronously(QueryWithEntityTypeInjectedAsync);

    public override void Query_with_EntityType_injected_into_property()
        => RunSynchronously(QueryWithEntityTypeInjectedIntoPropertyAsync);

    public override void Query_with_EntityType_injected_into_constructor_with_property()
        => RunSynchronously(QueryWithEntityTypeInjectedIntoConstructorWithPropertyAsync);

    public override void Attaching_entity_sets_EntityType()
        => RunSynchronously(AttachingEntitySetsEntityTypeAsync);

    public override void Query_with_StateManager_injected()
        => RunSynchronously(QueryWithStateManagerInjectedAsync);

    public override void Query_with_StateManager_injected_into_property()
        => RunSynchronously(QueryWithStateManagerInjectedIntoPropertyAsync);

    public override void Query_with_StateManager_injected_into_constructor_with_property()
        => RunSynchronously(QueryWithStateManagerInjectedIntoConstructorWithPropertyAsync);

    public override void Attaching_entity_sets_StateManager()
        => RunSynchronously(AttachingEntitySetsStateManagerAsync);

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_for_reference()
        => base.Query_with_loader_injected_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_for_collections()
        => base.Query_with_loader_injected_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_injected_for_reference_async()
        => base.Query_with_loader_injected_for_reference_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_injected_for_collections_async()
        => base.Query_with_loader_injected_for_collections_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_POCO_loader_injected_for_reference()
        => base.Query_with_POCO_loader_injected_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_POCO_loader_injected_for_collections()
        => base.Query_with_POCO_loader_injected_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_delegate_injected_for_reference_async()
        => base.Query_with_loader_delegate_injected_for_reference_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_delegate_injected_for_collections_async()
        => base.Query_with_loader_delegate_injected_for_collections_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_property_for_reference()
        => base.Query_with_loader_injected_into_property_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_property_for_collections()
        => base.Query_with_loader_injected_into_property_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Attaching_entity_sets_lazy_loader()
        => base.Attaching_entity_sets_lazy_loader();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Detaching_entity_resets_lazy_loader_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_so_it_can_be_reattached();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_field_for_reference()
        => base.Query_with_loader_injected_into_field_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_field_for_collections()
        => base.Query_with_loader_injected_into_field_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Attaching_entity_sets_lazy_loader_field()
        => base.Attaching_entity_sets_lazy_loader_field();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Detaching_entity_resets_lazy_loader_field_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_field_so_it_can_be_reattached();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Attaching_entity_sets_lazy_loader_delegate()
        => base.Attaching_entity_sets_lazy_loader_delegate();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Detaching_entity_resets_lazy_loader_delegate_so_it_can_be_reattached()
        => base.Detaching_entity_resets_lazy_loader_delegate_so_it_can_be_reattached();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_delegate_injected_into_property_for_reference()
        => base.Query_with_loader_delegate_injected_into_property_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_delgate_injected_into_property_for_collections()
        => base.Query_with_loader_delgate_injected_into_property_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_delegate_injected_into_property_for_reference_async()
        => base.Query_with_loader_delegate_injected_into_property_for_reference_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Query_with_loader_delegate_injected_into_property_for_collections_async()
        => base.Query_with_loader_delegate_injected_into_property_for_collections_async();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_property_via_constructor_for_reference()
        => base.Query_with_loader_injected_into_property_via_constructor_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Query_with_loader_injected_into_property_via_constructor_for_collections()
        => base.Query_with_loader_injected_into_property_via_constructor_for_collections();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void
        Query_with_loader_delegate_injected_into_property_via_constructor_for_reference()
        => base.Query_with_loader_delegate_injected_into_property_via_constructor_for_reference();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void
        Query_with_loader_delegate_injected_into_property_via_constructor_for_collections()
        => base.Query_with_loader_delegate_injected_into_property_via_constructor_for_collections();

    [ConditionalFact(Skip = SkipReason.GeneratedIntegerKeysNotSupported)]
    public override Task Add_immutable_record() => base.Add_immutable_record();

    private static void RunSynchronously(Func<Task> testCode)
        => testCode().GetAwaiter().GetResult();

    private static async Task<TEntity> SingleAsync<TEntity>(DbContext context) where TEntity : class
        => (await context.Set<TEntity>().AllowScan().ToListAsync()).Single();

    private async Task QueryWithContextInjectedAsync()
    {
        await using (var context = CreateContext())
        {
            Assert.Same(context, (await SingleAsync<HasContext<DbContext>>(context)).Context);
            Assert.Same(
                context,
                (await SingleAsync<HasContext<WithConstructorsContext>>(context)).Context);
            Assert.Null((await SingleAsync<HasContext<OtherContext>>(context)).Context);
        }

        await using (var context = CreateContext())
        {
            Assert.Same(context, (await SingleAsync<HasContext<DbContext>>(context)).Context);
            Assert.Same(
                context,
                (await SingleAsync<HasContext<WithConstructorsContext>>(context)).Context);
            Assert.Null((await SingleAsync<HasContext<OtherContext>>(context)).Context);
        }
    }

    private async Task QueryWithContextInjectedIntoPropertyAsync()
    {
        await using (var context = CreateContext())
        {
            Assert.Same(
                context,
                (await SingleAsync<HasContextProperty<DbContext>>(context)).Context);
            Assert.Same(
                context,
                (await SingleAsync<HasContextProperty<WithConstructorsContext>>(context)).Context);
            Assert.Null((await SingleAsync<HasContextProperty<OtherContext>>(context)).Context);
        }

        await using (var context = CreateContext())
        {
            Assert.Same(
                context,
                (await SingleAsync<HasContextProperty<DbContext>>(context)).Context);
            Assert.Same(
                context,
                (await SingleAsync<HasContextProperty<WithConstructorsContext>>(context)).Context);
            Assert.Null((await SingleAsync<HasContextProperty<OtherContext>>(context)).Context);
        }
    }

    private async Task QueryWithContextInjectedIntoConstructorWithPropertyAsync()
    {
        HasContextPc<DbContext> entityWithBase;
        HasContextPc<WithConstructorsContext> entityWithDerived;
        HasContextPc<OtherContext> entityWithOther;

        await using (var context = CreateContext())
        {
            entityWithBase = await SingleAsync<HasContextPc<DbContext>>(context);
            Assert.Same(context, entityWithBase.GetContext());
            Assert.False(entityWithBase.SetterCalled);

            entityWithDerived = await SingleAsync<HasContextPc<WithConstructorsContext>>(context);
            Assert.Same(context, entityWithDerived.GetContext());
            Assert.False(entityWithDerived.SetterCalled);

            entityWithOther = await SingleAsync<HasContextPc<OtherContext>>(context);
            Assert.Null(entityWithOther.GetContext());
            Assert.False(entityWithOther.SetterCalled);

            context.Entry(entityWithBase).State = EntityState.Detached;
            context.Entry(entityWithDerived).State = EntityState.Detached;
            context.Entry(entityWithOther).State = EntityState.Detached;

            Assert.Null(entityWithBase.GetContext());
            Assert.True(entityWithBase.SetterCalled);
            Assert.Null(entityWithDerived.GetContext());
            Assert.True(entityWithDerived.SetterCalled);
            Assert.Null(entityWithOther.GetContext());
            Assert.False(entityWithOther.SetterCalled);
        }

        await using (var context = CreateContext())
        {
            context.Attach(entityWithBase);
            context.Attach(entityWithDerived);
            context.Attach(entityWithOther);

            Assert.Same(context, entityWithBase.GetContext());
            Assert.True(entityWithBase.SetterCalled);
            Assert.Same(context, entityWithDerived.GetContext());
            Assert.True(entityWithDerived.SetterCalled);
            Assert.Null(entityWithOther.GetContext());
            Assert.False(entityWithOther.SetterCalled);
        }
    }

    private async Task AttachingEntitySetsContextAsync()
    {
        int id1, id2, id3;
        await using (var context = CreateContext())
        {
            id1 = (await SingleAsync<HasContextProperty<DbContext>>(context)).Id;
            id2 = (await SingleAsync<HasContextProperty<WithConstructorsContext>>(context)).Id;
            id3 = (await SingleAsync<HasContextProperty<OtherContext>>(context)).Id;
        }

        await using (var context = CreateContext())
        {
            var entityWithBase = new HasContextProperty<DbContext> { Id = id1 };
            var entityWithDerived = new HasContextProperty<WithConstructorsContext> { Id = id2 };
            var entityWithOther = new HasContextProperty<OtherContext> { Id = id3 };

            context.Attach(entityWithBase);
            context.Attach(entityWithDerived);
            context.Attach(entityWithOther);

            Assert.Same(context, entityWithBase.Context);
            Assert.Same(context, entityWithDerived.Context);
            Assert.Null(entityWithOther.Context);
        }
    }

    private async Task QueryWithEntityTypeInjectedAsync()
    {
        await using var context = CreateContext();
        Assert.Same(
            context.Model.FindEntityType(typeof(HasEntityType)),
            (await SingleAsync<HasEntityType>(context)).GetEntityType());
    }

    private async Task QueryWithEntityTypeInjectedIntoPropertyAsync()
    {
        await using var context = CreateContext();
        Assert.Same(
            context.Model.FindEntityType(typeof(HasEntityTypeProperty)),
            (await SingleAsync<HasEntityTypeProperty>(context)).EntityType);
    }

    private async Task QueryWithEntityTypeInjectedIntoConstructorWithPropertyAsync()
    {
        HasEntityTypePc entity;

        await using (var context = CreateContext())
        {
            entity = await SingleAsync<HasEntityTypePc>(context);

            Assert.Same(
                context.Model.FindEntityType(typeof(HasEntityTypePc)),
                entity.GetEntityType());
            Assert.False(entity.SetterCalled);

            context.Entry(entity).State = EntityState.Detached;

            Assert.Null(entity.GetEntityType());
            Assert.True(entity.SetterCalled);
        }

        await using (var context = CreateContext())
        {
            context.Attach(entity);

            Assert.Same(
                context.Model.FindEntityType(typeof(HasEntityTypePc)),
                entity.GetEntityType());
            Assert.True(entity.SetterCalled);
        }
    }

    private async Task AttachingEntitySetsEntityTypeAsync()
    {
        int id;
        await using (var context = CreateContext())
        {
            id = (await SingleAsync<HasEntityTypeProperty>(context)).Id;
        }

        await using (var context = CreateContext())
        {
            var entity = new HasEntityTypeProperty { Id = id };

            context.Attach(entity);

            Assert.Same(
                context.Model.FindEntityType(typeof(HasEntityTypeProperty)),
                entity.EntityType);
        }
    }

    private async Task QueryWithStateManagerInjectedAsync()
    {
        await using var context = CreateContext();
        Assert.Same(
            context.GetService<IStateManager>(),
            (await SingleAsync<HasStateManager>(context)).GetStateManager());
    }

    private async Task QueryWithStateManagerInjectedIntoPropertyAsync()
    {
        await using var context = CreateContext();
        Assert.Same(
            context.GetService<IStateManager>(),
            (await SingleAsync<HasStateManagerProperty>(context)).StateManager);
    }

    private async Task QueryWithStateManagerInjectedIntoConstructorWithPropertyAsync()
    {
        HasStateManagerPc entity;

        await using (var context = CreateContext())
        {
            entity = await SingleAsync<HasStateManagerPc>(context);

            Assert.Same(context.GetService<IStateManager>(), entity.GetStateManager());
            Assert.False(entity.SetterCalled);

            context.Entry(entity).State = EntityState.Detached;

            Assert.Null(entity.GetStateManager());
            Assert.True(entity.SetterCalled);
        }

        await using (var context = CreateContext())
        {
            context.Attach(entity);

            Assert.Same(context.GetService<IStateManager>(), entity.GetStateManager());
            Assert.True(entity.SetterCalled);
        }
    }

    private async Task AttachingEntitySetsStateManagerAsync()
    {
        int id;
        await using (var context = CreateContext())
        {
            id = (await SingleAsync<HasStateManagerProperty>(context)).Id;
        }

        await using (var context = CreateContext())
        {
            var entity = new HasStateManagerProperty { Id = id };

            context.Attach(entity);

            Assert.Same(context.GetService<IStateManager>(), entity.StateManager);
        }
    }

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
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.MappedEntityTypeIgnoredWarning));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            ConfigureServiceEntity<HasContext<DbContext>>(modelBuilder, "HasContextDbContext");
            ConfigureServiceEntity<HasContext<WithConstructorsContext>>(
                modelBuilder,
                "HasContextWithConstructorsContext");
            ConfigureServiceEntity<HasContext<OtherContext>>(
                modelBuilder,
                "HasContextOtherContext");

            ConfigureServiceEntity<HasContextProperty<DbContext>>(
                modelBuilder,
                "HasContextPropertyDbContext");
            ConfigureServiceEntity<HasContextProperty<WithConstructorsContext>>(
                modelBuilder,
                "HasContextPropertyWithConstructorsContext");
            ConfigureServiceEntity<HasContextProperty<OtherContext>>(
                modelBuilder,
                "HasContextPropertyOtherContext");

            ConfigureServiceEntity<HasContextPc<DbContext>>(modelBuilder, "HasContextPcDbContext");
            ConfigureServiceEntity<HasContextPc<WithConstructorsContext>>(
                modelBuilder,
                "HasContextPcWithConstructorsContext");
            ConfigureServiceEntity<HasContextPc<OtherContext>>(
                modelBuilder,
                "HasContextPcOtherContext");

            ConfigureServiceEntity<HasEntityType>(modelBuilder, "HasEntityType");
            ConfigureServiceEntity<HasEntityTypeProperty>(modelBuilder, "HasEntityTypeProperty");
            ConfigureServiceEntity<HasEntityTypePc>(modelBuilder, "HasEntityTypePc");

            ConfigureServiceEntity<HasStateManager>(modelBuilder, "HasStateManager");
            ConfigureServiceEntity<HasStateManagerProperty>(
                modelBuilder,
                "HasStateManagerProperty");
            ConfigureServiceEntity<HasStateManagerPc>(modelBuilder, "HasStateManagerPc");
        }

        protected override Task SeedAsync(WithConstructorsContext context)
        {
            context.AddRange(
                new HasContext<DbContext>(),
                new HasContext<WithConstructorsContext>(),
                new HasContext<OtherContext>());

            context.AddRange(
                new HasContextProperty<DbContext>(),
                new HasContextProperty<WithConstructorsContext>(),
                new HasContextProperty<OtherContext>());

            context.AddRange(
                new HasContextPc<DbContext>(),
                new HasContextPc<WithConstructorsContext>(),
                new HasContextPc<OtherContext>());

            context.AddRange(
                new HasEntityType(),
                new HasEntityTypeProperty(),
                new HasEntityTypePc());

            context.AddRange(
                new HasStateManager(),
                new HasStateManagerProperty(),
                new HasStateManagerPc());

            return context.SaveChangesAsync();
        }

        private static void ConfigureServiceEntity<TEntity>(
            ModelBuilder modelBuilder,
            string tableName) where TEntity : class
        {
            var entity = modelBuilder.Entity<TEntity>();
            entity.ToTable($"WithConstructors_{tableName}");
            entity.Property<int>("Id").ValueGeneratedNever();
            entity.HasPartitionKey("Id");
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class WithConstructorsDynamoTestDefault(
        WithConstructorsDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : WithConstructorsDynamoTest(fixture)
    {
        private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;
    }
}
