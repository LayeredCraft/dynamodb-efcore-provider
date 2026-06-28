using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class KeysWithConvertersDynamoTest
    : KeysWithConvertersTestBase<KeysWithConvertersDynamoTest.KeysWithConvertersDynamoFixture>
{
    protected KeysWithConvertersDynamoTest(KeysWithConvertersDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(KeysWithConvertersDynamoTest));

    [ConditionalFact]
    public async Task Can_query_and_find_with_converted_int_struct_partition_key()
    {
        await using (var context = CreateContext())
        {
            context.Add(new IntStructKeyPrincipal { Id = new IntStructKey(1), Foo = "One" });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var queried =
                await context
                    .Set<IntStructKeyPrincipal>()
                    .SingleAsync(e => e.Id.Equals(new IntStructKey(1)));
            Assert.Equal("One", queried.Foo);
        }

        await using (var context = CreateContext())
        {
            var found = await context.Set<IntStructKeyPrincipal>().FindAsync(new IntStructKey(1));
            Assert.Equal("One", found?.Foo);
        }

        AssertSql(
            """
            SELECT "id", "$type", "foo"
            FROM "IntStructKeyPrincipals"
            WHERE "id" = 1
            """,
            """
            SELECT "id", "$type", "foo"
            FROM "IntStructKeyPrincipals"
            WHERE "id" = ?
            """);
    }

    [ConditionalFact]
    public async Task Can_query_and_find_with_converted_binary_struct_partition_key()
    {
        var key = new byte[] { 1, 2, 3 };

        await using (var context = CreateContext())
        {
            context.Add(
                new BytesStructKeyPrincipal { Id = new BytesStructKey(key), Foo = "Binary" });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var queried =
                await context
                    .Set<BytesStructKeyPrincipal>()
                    .SingleAsync(e => e.Id.Equals(new BytesStructKey(key)));
            Assert.Equal("Binary", queried.Foo);
        }

        await using (var context = CreateContext())
        {
            var found =
                await context.Set<BytesStructKeyPrincipal>().FindAsync(new BytesStructKey(key));
            Assert.Equal("Binary", found?.Foo);
        }

        AssertSql(
            """
            SELECT "id", "$type", "foo"
            FROM "BytesStructKeyPrincipals"
            WHERE "id" = ?
            """,
            """
            SELECT "id", "$type", "foo"
            FROM "BytesStructKeyPrincipals"
            WHERE "id" = ?
            """);
    }

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_struct_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_struct_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_comparable_struct_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_key_and_optional_dependents()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_struct_key_and_required_dependents()
        => base.Can_insert_and_read_back_with_struct_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_key_and_required_dependents()
        => base.Can_insert_and_read_back_with_comparable_struct_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_key_and_required_dependents()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_class_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_bare_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_bare_class_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_comparable_class_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_structural_struct_binary_key_and_optional_dependents()
        => base
            .Can_insert_and_read_back_with_structural_struct_binary_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_binary_key_and_optional_dependents()
        => base
            .Can_insert_and_read_back_with_comparable_struct_binary_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_optional_dependents()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_optional_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_struct_binary_key_and_required_dependents()
        => base.Can_insert_and_read_back_with_struct_binary_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_binary_key_and_required_dependents()
        => base
            .Can_insert_and_read_back_with_comparable_struct_binary_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_structural_struct_binary_key_and_required_dependents()
        => base
            .Can_insert_and_read_back_with_structural_struct_binary_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_required_dependents()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_required_dependents();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_value_converter()
        => base.Can_query_and_update_owned_entity_with_value_converter();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_int_struct_key()
        => base.Can_query_and_update_owned_entity_with_int_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_binary_struct_key()
        => base.Can_query_and_update_owned_entity_with_binary_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_comparable_int_struct_key()
        => base.Can_query_and_update_owned_entity_with_comparable_int_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_comparable_bytes_struct_key()
        => base.Can_query_and_update_owned_entity_with_comparable_bytes_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_generic_comparable_int_struct_key()
        => base.Can_query_and_update_owned_entity_with_generic_comparable_int_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task
        Can_query_and_update_owned_entity_with_generic_comparable_bytes_struct_key()
        => base.Can_query_and_update_owned_entity_with_generic_comparable_bytes_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task
        Can_query_and_update_owned_entity_with_structural_generic_comparable_bytes_struct_key()
        => base
            .Can_query_and_update_owned_entity_with_structural_generic_comparable_bytes_struct_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_int_class_key()
        => base.Can_query_and_update_owned_entity_with_int_class_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_int_bare_class_key()
        => base.Can_query_and_update_owned_entity_with_int_bare_class_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_comparable_int_class_key()
        => base.Can_query_and_update_owned_entity_with_comparable_int_class_key();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Can_query_and_update_owned_entity_with_generic_comparable_int_class_key()
        => base.Can_query_and_update_owned_entity_with_generic_comparable_int_class_key();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_struct_key_and_optional_dependents_with_shadow_FK()
        => base.Can_insert_and_read_back_with_struct_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_comparable_struct_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_struct_key_and_required_dependents_with_shadow_FK()
        => base.Can_insert_and_read_back_with_struct_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_comparable_struct_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_class_key_and_optional_dependents_with_shadow_FK()
        => base.Can_insert_and_read_back_with_class_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_bare_class_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_bare_class_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_class_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_comparable_class_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_structural_struct_binary_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_structural_struct_binary_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_binary_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_comparable_struct_binary_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_optional_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_struct_binary_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_struct_binary_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_comparable_struct_binary_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_comparable_struct_binary_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_structural_struct_binary_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_structural_struct_binary_key_and_required_dependents_with_shadow_FK();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task
        Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_required_dependents_with_shadow_FK()
        => base
            .Can_insert_and_read_back_with_generic_comparable_struct_binary_key_and_required_dependents_with_shadow_FK();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    public class KeysWithConvertersDynamoFixture
        : KeysWithConvertersFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override bool UseInclude => false;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.MappedNavigationIgnoredWarning,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            ConfigureIntStructPrincipal(modelBuilder);
            ConfigureBytesStructPrincipal(modelBuilder);
        }

        private static void ConfigureIntStructPrincipal(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntStructKeyPrincipal>(entity =>
            {
                entity.ToTable("IntStructKeyPrincipals").HasPartitionKey(e => e.Id);
                entity
                    .Property(e => e.Id)
                    .HasConversion(IntStructKey.Converter)
                    .ValueGeneratedNever();
                entity.Ignore(e => e.OptionalDependents);
                entity.Ignore(e => e.RequiredDependents);
            });

        private static void ConfigureBytesStructPrincipal(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BytesStructKeyPrincipal>(entity =>
            {
                entity.ToTable("BytesStructKeyPrincipals").HasPartitionKey(e => e.Id);
                entity
                    .Property(e => e.Id)
                    .HasConversion(BytesStructKey.Converter)
                    .ValueGeneratedNever();
                entity.Ignore(e => e.OptionalDependents);
                entity.Ignore(e => e.RequiredDependents);
            });
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class KeysWithConvertersDynamoTestDefault : KeysWithConvertersDynamoTest
    {
        public KeysWithConvertersDynamoTestDefault(
            KeysWithConvertersDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
