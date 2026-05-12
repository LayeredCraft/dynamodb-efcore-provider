using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Specification find tests for the DynamoDB provider.</summary>
public abstract class FindDynamoTest(FindDynamoTest.FindDynamoFixture fixture)
    : FindTestBase<FindDynamoTest.FindDynamoFixture>(fixture)
{
    public override void Find_int_key_from_store()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Find_int_key_from_store());

    public override async Task Find_int_key_from_store_async(CancellationType cancellationType)
    {
        await base.Find_int_key_from_store_async(cancellationType);

        AssertSql(
            """
            SELECT "Id", "Foo"
            FROM "IntKey"
            WHERE "Id" = ?
            """);
    }

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    public class FindDynamoTestSet(FindDynamoFixture fixture) : FindDynamoTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaSetFinder();
    }

    public class FindDynamoFixture : FindFixtureBase
    {
        private readonly DynamoTestStoreFactory _testStoreFactory = new();

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => _testStoreFactory;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(options => options.DynamoDbClient(_testStoreFactory.Client));

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
