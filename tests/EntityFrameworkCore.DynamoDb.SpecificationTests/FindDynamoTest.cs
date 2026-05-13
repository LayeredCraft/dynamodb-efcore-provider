using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Specification find tests for the DynamoDB provider.</summary>
public abstract class FindDynamoTest : FindTestBase<FindDynamoTest.FindDynamoFixture>
{
    private const string CompositeKeysNotSupported = "DynamoDB does not support composite keys.";
    private const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    private const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";

    protected FindDynamoTest(FindDynamoFixture fixture) : base(fixture) => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(FindDynamoTest));

    public override void Find_int_key_from_store()
        => NoSyncTest(() => base.Find_int_key_from_store());

    public override void Returns_null_for_int_key_not_in_store()
        => NoSyncTest(() => base.Returns_null_for_int_key_not_in_store());

    [ConditionalFact(Skip = NullableKeysNotSupported)]
    public override void Find_nullable_int_key_tracked() { }

    [ConditionalFact(Skip = NullableKeysNotSupported)]
    public override void Find_nullable_int_key_from_store() { }

    [ConditionalFact(Skip = NullableKeysNotSupported)]
    public override void Returns_null_for_nullable_int_key_not_in_store() { }

    public override void Find_string_key_from_store()
        => NoSyncTest(() => base.Find_string_key_from_store());

    public override void Returns_null_for_string_key_not_in_store()
        => NoSyncTest(() => base.Returns_null_for_string_key_not_in_store());

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Find_composite_key_tracked() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Find_composite_key_from_store() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Returns_null_for_composite_key_not_in_store() { }

    public override void Find_base_type_from_store()
        => NoSyncTest(() => base.Find_base_type_from_store());

    public override void Returns_null_for_base_type_not_in_store()
        => NoSyncTest(() => base.Returns_null_for_base_type_not_in_store());

    public override void Find_derived_type_from_store()
        => NoSyncTest(() => base.Find_derived_type_from_store());

    public override void Returns_null_for_derived_type_not_in_store()
        => NoSyncTest(() => base.Returns_null_for_derived_type_not_in_store());

    public override void Find_base_type_using_derived_set_tracked()
        => NoSyncTest(() => base.Find_base_type_using_derived_set_tracked());

    public override void Find_base_type_using_derived_set_from_store()
        => NoSyncTest(() => base.Find_base_type_using_derived_set_from_store());

    public override void Find_derived_using_base_set_type_from_store()
        => NoSyncTest(() => base.Find_derived_using_base_set_type_from_store());

    public override void Find_int_key_tracked()
    {
        base.Find_int_key_tracked();

        AssertSql();
    }

    public override void Find_string_key_tracked()
    {
        base.Find_string_key_tracked();

        AssertSql();
    }

    public override void Find_base_type_tracked()
    {
        base.Find_base_type_tracked();

        AssertSql();
    }

    public override void Find_derived_type_tracked()
    {
        base.Find_derived_type_tracked();

        AssertSql();
    }

    public override void Find_derived_type_using_base_set_tracked()
    {
        base.Find_derived_type_using_base_set_tracked();

        AssertSql();
    }

    public override void Returns_null_for_null_key()
    {
        base.Returns_null_for_null_key();

        AssertSql();
    }

    public override void Throws_for_multiple_values_passed_for_simple_key()
    {
        base.Throws_for_multiple_values_passed_for_simple_key();

        AssertSql();
    }

    public override void Throws_for_bad_type_for_simple_key()
    {
        base.Throws_for_bad_type_for_simple_key();

        AssertSql();
    }

    public override void Throws_for_bad_entity_type()
    {
        base.Throws_for_bad_entity_type();

        AssertSql();
    }

    [ConditionalFact(Skip = ShadowKeysNotSupported)]
    public override void Find_shadow_key_tracked() { }

    [ConditionalFact(Skip = ShadowKeysNotSupported)]
    public override void Find_shadow_key_from_store() { }

    [ConditionalFact(Skip = ShadowKeysNotSupported)]
    public override void Returns_null_for_shadow_key_not_in_store() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Returns_null_for_null_key_values_array() { }

    [ConditionalFact(Skip = NullableKeysNotSupported)]
    public override void Returns_null_for_null_nullable_key() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Returns_null_for_null_in_composite_key() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Throws_for_wrong_number_of_values_for_composite_key() { }

    [ConditionalFact(Skip = CompositeKeysNotSupported)]
    public override void Throws_for_bad_type_for_composite_key() { }

    [ConditionalFact(Skip = ShadowKeysNotSupported)]
    public override void Throws_for_bad_entity_type_with_different_namespace() { }

    [ConditionalTheory(Skip = NullableKeysNotSupported)]
    public override Task Find_nullable_int_key_tracked_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = NullableKeysNotSupported)]
    public override Task Find_nullable_int_key_from_store_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = NullableKeysNotSupported)]
    public override Task Returns_null_for_nullable_int_key_not_in_store_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Find_composite_key_tracked_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Find_composite_key_from_store_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Returns_null_for_composite_key_not_in_store_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = ShadowKeysNotSupported)]
    public override Task Find_shadow_key_tracked_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = ShadowKeysNotSupported)]
    public override Task Find_shadow_key_from_store_async(CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = ShadowKeysNotSupported)]
    public override Task Returns_null_for_shadow_key_not_in_store_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Returns_null_for_null_key_values_array_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Returns_null_for_null_in_composite_key_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Throws_for_wrong_number_of_values_for_composite_key_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = CompositeKeysNotSupported)]
    public override Task Throws_for_bad_type_for_composite_key_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = ShadowKeysNotSupported)]
    public override Task Throws_for_bad_entity_type_with_different_namespace_async(
        CancellationType cancellationType)
        => Task.CompletedTask;

    public override async Task Find_int_key_from_store_async(CancellationType cancellationType)
    {
        await base.Find_int_key_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "foo", "ownedCollection", "ownedReference"
            FROM "ints"
            WHERE "id" = ?
            """);
    }

    public override async Task Returns_null_for_int_key_not_in_store_async(
        CancellationType cancellationType)
    {
        await base.Returns_null_for_int_key_not_in_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "foo", "ownedCollection", "ownedReference"
            FROM "ints"
            WHERE "id" = ?
            """);
    }

    public override async Task Find_string_key_from_store_async(CancellationType cancellationType)
    {
        await base.Find_string_key_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "foo"
            FROM "strings"
            WHERE "id" = ?
            """);
    }

    public override async Task Returns_null_for_string_key_not_in_store_async(
        CancellationType cancellationType)
    {
        await base.Returns_null_for_string_key_not_in_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "foo"
            FROM "strings"
            WHERE "id" = ?
            """);
    }

    public override async Task Find_base_type_from_store_async(CancellationType cancellationType)
    {
        await base.Find_base_type_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND ("$type" = 'BaseType' OR "$type" = 'DerivedType')
            """);
    }

    public override async Task Returns_null_for_base_type_not_in_store_async(
        CancellationType cancellationType)
    {
        await base.Returns_null_for_base_type_not_in_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND ("$type" = 'BaseType' OR "$type" = 'DerivedType')
            """);
    }

    public override async Task Find_derived_type_from_store_async(CancellationType cancellationType)
    {
        await base.Find_derived_type_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND "$type" = 'DerivedType'
            """);
    }

    public override async Task Returns_null_for_derived_type_not_in_store_async(
        CancellationType cancellationType)
    {
        await base.Returns_null_for_derived_type_not_in_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND "$type" = 'DerivedType'
            """);
    }

    public override async Task Find_base_type_using_derived_set_tracked_async(
        CancellationType cancellationType)
    {
        await base.Find_base_type_using_derived_set_tracked_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND "$type" = 'DerivedType'
            """);
    }

    public override async Task Find_base_type_using_derived_set_from_store_async(
        CancellationType cancellationType)
    {
        await base.Find_base_type_using_derived_set_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND "$type" = 'DerivedType'
            """);
    }

    public override async Task Find_derived_using_base_set_type_from_store_async(
        CancellationType cancellationType)
    {
        await base.Find_derived_using_base_set_type_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "id", "$type", "foo", "boo"
            FROM "base"
            WHERE "id" = ? AND ("$type" = 'BaseType' OR "$type" = 'DerivedType')
            """);
    }

    public override async Task Find_int_key_tracked_async(CancellationType cancellationType)
    {
        await base.Find_int_key_tracked_async(cancellationType);

        AssertSql();
    }

    public override async Task Find_string_key_tracked_async(CancellationType cancellationType)
    {
        await base.Find_string_key_tracked_async(cancellationType);

        AssertSql();
    }

    public override async Task Find_base_type_tracked_async(CancellationType cancellationType)
    {
        await base.Find_base_type_tracked_async(cancellationType);

        AssertSql();
    }

    public override async Task Find_derived_type_tracked_async(CancellationType cancellationType)
    {
        await base.Find_derived_type_tracked_async(cancellationType);

        AssertSql();
    }

    public override async Task Find_derived_type_using_base_set_tracked_async(
        CancellationType cancellationType)
    {
        await base.Find_derived_type_using_base_set_tracked_async(cancellationType);

        AssertSql();
    }

    public override async Task Returns_null_for_null_key_async(CancellationType cancellationType)
    {
        await base.Returns_null_for_null_key_async(cancellationType);

        AssertSql();
    }

    public override async Task Throws_for_multiple_values_passed_for_simple_key_async(
        CancellationType cancellationType)
    {
        await base.Throws_for_multiple_values_passed_for_simple_key_async(cancellationType);

        AssertSql();
    }

    public override async Task Throws_for_bad_type_for_simple_key_async(
        CancellationType cancellationType)
    {
        await base.Throws_for_bad_type_for_simple_key_async(cancellationType);

        AssertSql();
    }

    public override async Task Throws_for_bad_entity_type_async(CancellationType cancellationType)
    {
        await base.Throws_for_bad_entity_type_async(cancellationType);

        AssertSql();
    }

    //      ╭──────────────────────────────────────────────────────────╮
    //      │                        Test Infra                        │
    //      ╰──────────────────────────────────────────────────────────╯

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private void NoSyncTest(Action testCode) => DynamoTestHelpers.Instance.NoSyncTest(testCode);

    public class FindDynamoTestSet(FindDynamoFixture fixture) : FindDynamoTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaSetFinder();
    }

    public class FindDynamoFixture : FindFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Entity<IntKey>().ToTable("ints").HasPartitionKey(x => x.Id);

            modelBuilder.Entity<StringKey>().ToTable("strings").HasPartitionKey(x => x.Id);

            modelBuilder.Entity<BaseType>().ToTable("base").HasPartitionKey(x => x.Id);

            modelBuilder.Entity<DerivedType>();
        }

        protected override Task SeedAsync(PoolableDbContext context)
        {
            context.AddRange(
                new IntKey
                {
                    Id = 77,
                    Foo = "Smokey",
                    OwnedReference =
                        new Owned1
                        {
                            Prop = 7,
                            NestedOwned = new Owned2 { Prop = "7" },
                            NestedOwnedCollection =
                            [
                                new Owned2 { Prop = "71" }, new Owned2 { Prop = "72" },
                            ],
                        },
                    OwnedCollection = [new Owned1 { Prop = 71 }, new Owned1 { Prop = 72 }],
                },
                new StringKey { Id = "Cat", Foo = "Alice" },
                new BaseType { Id = 77, Foo = "Baxter" },
                new DerivedType { Id = 78, Foo = "Strawberry", Boo = "Cheesecake" });

            return context.SaveChangesAsync();
        }
    }
}
