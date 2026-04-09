using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Unit tests for write-path error handling in the serializer source and helpers.</summary>
public class DynamoEntityItemSerializerSourceTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Table name sanitization
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     <c>BuildInsertStatement</c> must reject a table name containing a double-quote because
    ///     that character would break the PartiQL identifier syntax.
    /// </summary>
    [Fact]
    public async Task SaveChanges_TableNameWithDoubleQuote_ThrowsArgumentException()
    {
        var options = new DbContextOptionsBuilder<QuotedTableDbContext>().UseDynamo().Options;

        await using var db = new QuotedTableDbContext(options);
        db.Gadgets.Add(new Gadget { Pk = "G#1", Sk = "G1" });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SaveChangesAsync());

        ex.Message.Should().Contain("\"");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Fixtures
    // ──────────────────────────────────────────────────────────────────────────────

    private sealed class Gadget
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
    }

    private sealed class QuotedTableDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Gadget> Gadgets => Set<Gadget>();

        // Table name contains a double-quote which is illegal in the PartiQL identifier.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gadget>(b =>
            {
                b.ToTable("Bad\"TableName");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });
    }
}
