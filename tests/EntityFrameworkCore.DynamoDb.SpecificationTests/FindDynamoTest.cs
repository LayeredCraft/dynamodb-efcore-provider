using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Specification find tests for the DynamoDB provider.</summary>
public abstract class FindDynamoTest : FindTestBase<FindDynamoTest.FindDynamoFixture>
{
    protected FindDynamoTest(FindDynamoFixture fixture) : base(fixture) => fixture.ClearSql();

    public override void Find_int_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_int_key_from_store());

    public override void Returns_null_for_int_key_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_int_key_not_in_store());

    public override void Find_nullable_int_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_nullable_int_key_from_store());

    public override void Returns_null_for_nullable_int_key_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_nullable_int_key_not_in_store());

    public override void Find_string_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_string_key_from_store());

    public override void Returns_null_for_string_key_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_string_key_not_in_store());

    public override void Find_composite_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_composite_key_from_store());

    public override void Returns_null_for_composite_key_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_composite_key_not_in_store());

    public override void Find_base_type_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_base_type_from_store());

    public override void Returns_null_for_base_type_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_base_type_not_in_store());

    public override void Find_derived_type_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_derived_type_from_store());

    public override void Returns_null_for_derived_type_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_derived_type_not_in_store());

    public override void Find_base_type_using_derived_set_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Find_base_type_using_derived_set_from_store());

    public override void Find_derived_using_base_set_type_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Find_derived_using_base_set_type_from_store());

    public override void Find_shadow_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_shadow_key_from_store());

    public override void Returns_null_for_shadow_key_not_in_store()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Returns_null_for_shadow_key_not_in_store());

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

    //      ╭──────────────────────────────────────────────────────────╮
    //      │                        Test Infra                        │
    //      ╰──────────────────────────────────────────────────────────╯

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

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
