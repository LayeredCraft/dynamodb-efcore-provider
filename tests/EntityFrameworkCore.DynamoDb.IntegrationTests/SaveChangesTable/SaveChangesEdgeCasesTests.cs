using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests for DynamoDB- and PartiQL-specific edge cases outside the happy-path
///     CRUD stories: no-op saves, <c>acceptAllChangesOnSuccess</c> semantics, and the statement-size
///     guard.
/// </summary>
public class SaveChangesEdgeCasesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    // ── No-op ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ZeroChanges_ReturnsZero_AndNoWritesEmitted()
    {
        // Fresh context with nothing tracked — SaveChanges should be a no-op.
        var affected = await Db.SaveChangesAsync(CancellationToken);

        affected.Should().Be(0);
        AssertSql(); // No PartiQL statements should have been emitted.
    }

    // ── acceptAllChangesOnSuccess = false ─────────────────────────────────────

    [Fact]
    public async Task AcceptAllChangesFalse_SingleRoot_PersistsButKeepsEntryPending()
    {
        // EF Core standard behavior: SaveChanges(false) persists the write but skips the
        // AcceptAllChanges() call, leaving entries in their pre-save state until the caller
        // explicitly calls ChangeTracker.AcceptAllChanges().
        const string pk = "TENANT#EDGE";
        const string sk = "CUSTOMER#ACCEPT-FALSE";

        var customer = new CustomerItem
        {
            Pk = pk,
            Sk = sk,
            Version = 1,
            Email = "edge@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };

        Db.Customers.Add(customer);

        // SaveChanges with acceptAllChangesOnSuccess = false.
        var affected = await Db.SaveChangesAsync(false, CancellationToken);

        // Write reached DynamoDB.
        affected.Should().Be(1);
        (await GetItemAsync(pk, sk, CancellationToken)).Should().NotBeNull();

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'Contacts': ?}
            """);

        // Entry still in Added state — AcceptAllChanges was not called.
        Db.Entry(customer).State.Should().Be(EntityState.Added);

        // After explicit AcceptAllChanges, entry becomes Unchanged.
        Db.ChangeTracker.AcceptAllChanges();
        Db.Entry(customer).State.Should().Be(EntityState.Unchanged);
    }

    // ── Statement-length guard ────────────────────────────────────────────────

    [Fact]
    public async Task Insert_WithStatementExceeding8192Chars_ThrowsBeforeAnyWrite()
    {
        // This test verifies the provider's PartiQL statement-size guard fires before
        // any write reaches DynamoDB. The LongStatementItem entity maps 15 scalar properties
        // to attribute names that are each 600 characters long. The resulting INSERT statement
        // comfortably exceeds DynamoDB's 8192-byte statement-size limit.
        const string pk = "TENANT#EDGE";
        const string sk = "ITEM#LONG-STATEMENT";

        var item = new LongStatementItem { Pk = pk, Sk = sk };

        var act = async () =>
        {
            await using var context = CreateLongStatementContext();
            context.Items.Add(item);
            await context.SaveChangesAsync(CancellationToken);
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*8192*");

        // Guard fired before the write — item must not exist in DynamoDB.
        (await GetItemAsync(pk, sk, CancellationToken)).Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LongStatementContext CreateLongStatementContext()
        => new(
            new DbContextOptionsBuilder<LongStatementContext>()
                .UseDynamo(options => options.DynamoDbClient(Client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    // ── Private model ─────────────────────────────────────────────────────────

    /// <summary>
    ///     A purpose-built entity whose DynamoDB attribute names are intentionally long so that the
    ///     generated INSERT statement exceeds DynamoDB's 8192-byte statement-size limit.
    /// </summary>
    private sealed class LongStatementItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string? P01 { get; set; }
        public string? P02 { get; set; }
        public string? P03 { get; set; }
        public string? P04 { get; set; }
        public string? P05 { get; set; }
        public string? P06 { get; set; }
        public string? P07 { get; set; }
        public string? P08 { get; set; }
        public string? P09 { get; set; }
        public string? P10 { get; set; }
        public string? P11 { get; set; }
        public string? P12 { get; set; }
        public string? P13 { get; set; }
        public string? P14 { get; set; }
        public string? P15 { get; set; }
    }

    /// <summary>
    ///     DbContext that maps <see cref="LongStatementItem" /> with exaggerated attribute names to
    ///     reliably trigger the statement-length guard without requiring hundreds of properties.
    /// </summary>
    private sealed class LongStatementContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<LongStatementItem> Items => Set<LongStatementItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Each attribute name is 600 characters long. With 15 properties the INSERT
            // statement will be ~9100 characters (bytes for ASCII), well above the 8192-byte guard.
            const string LongName =
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
                + // 100
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
                + // 100
                "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
                + // 100
                "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD"
                + // 100
                "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE"
                + // 100
                "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"; // 100

            modelBuilder.Entity<LongStatementItem>(builder =>
            {
                builder.ToTable(SaveChangesItemTable.TableName);
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);

                builder.Property(x => x.P01).HasAttributeName(LongName + "_01");
                builder.Property(x => x.P02).HasAttributeName(LongName + "_02");
                builder.Property(x => x.P03).HasAttributeName(LongName + "_03");
                builder.Property(x => x.P04).HasAttributeName(LongName + "_04");
                builder.Property(x => x.P05).HasAttributeName(LongName + "_05");
                builder.Property(x => x.P06).HasAttributeName(LongName + "_06");
                builder.Property(x => x.P07).HasAttributeName(LongName + "_07");
                builder.Property(x => x.P08).HasAttributeName(LongName + "_08");
                builder.Property(x => x.P09).HasAttributeName(LongName + "_09");
                builder.Property(x => x.P10).HasAttributeName(LongName + "_10");
                builder.Property(x => x.P11).HasAttributeName(LongName + "_11");
                builder.Property(x => x.P12).HasAttributeName(LongName + "_12");
                builder.Property(x => x.P13).HasAttributeName(LongName + "_13");
                builder.Property(x => x.P14).HasAttributeName(LongName + "_14");
                builder.Property(x => x.P15).HasAttributeName(LongName + "_15");
            });
        }
    }
}
