using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Unit tests for write-path error handling in the serializer source and helpers.</summary>
public class DynamoEntityItemSerializerSourceTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Converter cast diagnostics
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     When the boxed-converter-path set serializer receives a value that doesn't match the
    ///     declared provider type, it must throw with a clear message naming both the actual and expected
    ///     types — not an opaque <see cref="InvalidCastException" />.
    /// </summary>
    [Fact]
    public void SerializeSet_ConverterReturnsWrongType_ThrowsInvalidOperationWithTypeNames()
    {
        // Converter declared as string → string but actually returning int at runtime.
        var mismatchingConverter = (object? v) => (object?)42;

        var act = () => DynamoAttributeValueCollectionHelpers.SerializeSet<string>(
            new List<string> { "a", "b" },
            mismatchingConverter);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Int32"); // actual type returned
        ex.Message.Should().Contain("String"); // expected TProvider
    }

    /// <summary>
    ///     When a boxed-converter-path list serializer receives a value that doesn't match the
    ///     declared provider type, it must throw with a clear message rather than an opaque cast
    ///     exception.
    /// </summary>
    [Fact]
    public void SerializeList_ConverterReturnsWrongType_ThrowsInvalidOperationWithTypeNames()
    {
        // Converter declared as string → string but actually returning a DateTimeOffset.
        var mismatchingConverter = (object? v)
            => (object?)new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var act = () => DynamoAttributeValueCollectionHelpers.SerializeList<string>(
            new List<string> { "x" },
            mismatchingConverter);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("DateTimeOffset"); // actual type returned
        ex.Message.Should().Contain("String"); // expected TProvider
    }

    /// <summary>
    ///     When a boxed-converter-path dictionary serializer receives a value that doesn't match the
    ///     declared provider type, it must throw with a clear message.
    /// </summary>
    [Fact]
    public void SerializeDictionary_ConverterReturnsWrongType_ThrowsInvalidOperationWithTypeNames()
    {
        var dict = new Dictionary<string, string> { ["k"] = "v" };
        // Converter declared as string → string but actually returning a Guid.
        var mismatchingConverter = (object? v) => (object?)Guid.NewGuid();

        var act = () => DynamoAttributeValueCollectionHelpers.SerializeDictionary<string>(
            dict,
            mismatchingConverter);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Guid"); // actual type returned
        ex.Message.Should().Contain("String"); // expected TProvider
    }

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
