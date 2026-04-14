using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests that verify unsupported write model shapes fail before any write is
///     sent.
/// </summary>
public class SaveChangesModelValidationTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    [Fact]
    public async Task SaveChanges_UnmappedScalarProperty_ThrowsBeforeWrite()
    {
        var item = new UnmappedScalarItem
        {
            Pk = "TENANT#VALIDATION",
            Sk = "MODEL#UNMAPPED-SCALAR",
            Metadata = new CustomPayload(),
        };

        var act = async () =>
        {
            await using var context = CreateUnmappedScalarContext();
            context.Items.Add(item);
            await context.SaveChangesAsync(CancellationToken);
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*HasConversion*");

        (await GetItemAsync(item.Pk, item.Sk, CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_RowVersionConcurrencyShape_ThrowsBeforeWrite()
    {
        var item = new RowVersionItem
        {
            Pk = "TENANT#VALIDATION", Sk = "MODEL#ROWVERSION", Token = 1,
        };

        var act = async () =>
        {
            await using var context = CreateRowVersionContext();
            context.Items.Add(item);
            await context.SaveChangesAsync(CancellationToken);
        };

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IsRowVersion()*not currently support*");

        (await GetItemAsync(item.Pk, item.Sk, CancellationToken)).Should().BeNull();
    }

    private UnmappedScalarContext CreateUnmappedScalarContext()
        => new(
            new DbContextOptionsBuilder<UnmappedScalarContext>().UseDynamo(options
                    => options.DynamoDbClient(Client))
                .Options);

    private RowVersionContext CreateRowVersionContext()
        => new(
            new DbContextOptionsBuilder<RowVersionContext>().UseDynamo(options
                    => options.DynamoDbClient(Client))
                .Options);

    private sealed class UnmappedScalarContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UnmappedScalarItem> Items => Set<UnmappedScalarItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnmappedScalarItem>(builder =>
            {
                builder.ToTable(SaveChangesTableDynamoFixture.TableName);
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
                builder.Property(x => x.Metadata);
            });
    }

    private sealed class RowVersionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RowVersionItem> Items => Set<RowVersionItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RowVersionItem>(builder =>
            {
                builder.ToTable(SaveChangesTableDynamoFixture.TableName);
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
                builder.Property(x => x.Token).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
            });
    }

    private sealed class UnmappedScalarItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public CustomPayload Metadata { get; set; } = null!;
    }

    private sealed class RowVersionItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public long Token { get; set; }
    }

    private sealed class CustomPayload { }
}
