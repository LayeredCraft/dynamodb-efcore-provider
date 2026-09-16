using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.NativeAotTests;

public sealed class AotRuntimeContext : DbContext
{
    internal const string DesignTimeTableName = "AotRuntimeItems";
    internal const string RuntimeTableName = "AotRuntimeConfiguredItems-runtime";

    internal static string TableName
        => Environment.GetEnvironmentVariable("DYNAMO_AOT_TEST_RUNTIME_TABLE_NAME")
            ?? DesignTimeTableName;

    public DbSet<AotRuntimeItem> Items => Set<AotRuntimeItem>();

    internal static void ConfigureRuntimeTableName(string tableName)
    {
        using var context = new AotRuntimeContext();
        if (context.Model.FindEntityType(typeof(AotRuntimeItem)) is not RuntimeEntityType
            entityType)
            throw new InvalidOperationException(
                "The generated runtime model is missing the item entity type.");

        entityType.SetAnnotation("Dynamo:TableName", tableName);
        entityType.SetRuntimeAnnotation("Dynamo:TableGroupName", tableName);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(providerOptions
            => providerOptions.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL =
                    Environment.GetEnvironmentVariable(DynamoFixture.ServiceUrlEnvironmentVariable)
                    ?? "http://127.0.0.1:9";
                config.AuthenticationRegion = "us-east-1";
            }));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<AotRuntimeItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, TableName);
            entity.HasPartitionKey(item => item.Pk);
            DynamoEntityTypeBuilderExtensions.HasSortKey(entity, item => item.Sk);
            entity.Property(item => item.Status).HasConversion<string>();
        });
}

public sealed class AotRuntimeItem
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Name { get; set; } = null!;
    public AotRuntimeStatus Status { get; set; }
    public AotRuntimeStatus RawStatus { get; set; }
    public AotRuntimeStatus? OptionalStatus { get; set; }
    public int? Count { get; set; }
    public bool Enabled { get; set; }
    public byte[] Payload { get; set; } = null!;
    public string[] Aliases { get; set; } = [];
}

public enum AotRuntimeStatus
{
    Active,
    Inactive
}

internal static class AotRuntimeData
{
    public static AotRuntimeItem[] CreateSeedItems()
        =>
        [
            new()
            {
                Pk = "tenant-1",
                Sk = "sk-1",
                Name = "Native",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Inactive,
                OptionalStatus = AotRuntimeStatus.Active,
                Count = 42,
                Enabled = true,
                Payload = [1, 2, 3],
                Aliases = ["aot"]
            },
            new()
            {
                Pk = "tenant-null",
                Sk = "sk-null",
                Name = "Nullable",
                Status = AotRuntimeStatus.Inactive,
                RawStatus = AotRuntimeStatus.Inactive,
                OptionalStatus = null,
                Count = null,
                Enabled = false,
                Payload = [0],
                Aliases = ["null"]
            },
            new()
            {
                Pk = "tenant-limit",
                Sk = "sk-limit-1",
                Name = "Limit1",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 1,
                Enabled = true,
                Payload = [1],
                Aliases = ["limit"]
            },
            new()
            {
                Pk = "tenant-limit",
                Sk = "sk-limit-2",
                Name = "Limit2",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 2,
                Enabled = true,
                Payload = [2],
                Aliases = ["limit"]
            },
            new()
            {
                Pk = "tenant-limit",
                Sk = "sk-limit-3",
                Name = "Limit3",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 3,
                Enabled = true,
                Payload = [3],
                Aliases = ["limit"]
            },
            new()
            {
                Pk = "tenant-limit",
                Sk = "sk-limit-4",
                Name = "Limit4",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 4,
                Enabled = true,
                Payload = [4],
                Aliases = ["limit"]
            },
            new()
            {
                Pk = "tenant-page",
                Sk = "sk-page-1",
                Name = "Page1",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 1,
                Enabled = true,
                Payload = [1],
                Aliases = ["page"]
            },
            new()
            {
                Pk = "tenant-page",
                Sk = "sk-page-2",
                Name = "Page2",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 2,
                Enabled = true,
                Payload = [2],
                Aliases = ["page"]
            },
            new()
            {
                Pk = "tenant-page",
                Sk = "sk-page-3",
                Name = "Page3",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 3,
                Enabled = true,
                Payload = [3],
                Aliases = ["page"]
            },
            new()
            {
                Pk = "tenant-page",
                Sk = "sk-page-4",
                Name = "Page4",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 4,
                Enabled = true,
                Payload = [4],
                Aliases = ["page"]
            },
            new()
            {
                Pk = "tenant-page",
                Sk = "sk-page-5",
                Name = "Page5",
                Status = AotRuntimeStatus.Active,
                RawStatus = AotRuntimeStatus.Active,
                Count = 5,
                Enabled = true,
                Payload = [5],
                Aliases = ["page"]
            }
        ];

    public static AotRuntimeItem ExpectedNativeItem
        => new()
        {
            Pk = "tenant-1",
            Sk = "sk-1",
            Name = "Native",
            Status = AotRuntimeStatus.Active,
            RawStatus = AotRuntimeStatus.Inactive,
            OptionalStatus = AotRuntimeStatus.Active,
            Count = 42,
            Enabled = true,
            Payload = [1, 2, 3],
            Aliases = ["aot"]
        };

    public static AotRuntimeItem ExpectedNullableItem
        => new()
        {
            Pk = "tenant-null",
            Sk = "sk-null",
            Name = "Nullable",
            Status = AotRuntimeStatus.Inactive,
            RawStatus = AotRuntimeStatus.Inactive,
            Count = null,
            Enabled = false,
            Payload = [0],
            Aliases = ["null"]
        };
}

public static class AotRuntimeQueries
{
    private const string PagePartitionKey = "tenant-page";

    public static async Task<List<AotRuntimeItem>> LoadItemsAsync()
    {
        await using var context = new AotRuntimeContext();
        string[] partitionKeys = ["tenant-1", "tenant-2"];
        return await context
            .Items
            .Where(item => ((IEnumerable<string>)partitionKeys).Contains(item.Pk))
            .ToListAsync();
    }

    public static async Task<List<AotRuntimeItem>> LoadActiveItemsAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-1";
        var active = AotRuntimeStatus.Active;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Status == active)
            .ToListAsync();
    }

    public static async Task<List<AotRuntimeItem>> LoadItemsByCountAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-1";
        int count = 42;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Count == count)
            .ToListAsync();
    }

    public static async Task<List<AotRuntimeItem>> LoadItemsBySortKeyAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-1";
        string sortKey = "sk-1";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Sk == sortKey)
            .ToListAsync();
    }

    public static async Task<AotRuntimeStatus> ProjectConvertedStatusAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-1";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Select(item => item.Status)
            .FirstAsync();
    }

    public static async Task<List<AotRuntimeItem>> LoadItemsWithNullCountAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-null";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Count == null)
            .ToListAsync();
    }

    public static async Task<List<AotRuntimeStatus>> ProjectRawStatusAsync()
    {
        await using var context = new AotRuntimeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1")
            .Select(item => item.RawStatus)
            .ToListAsync();
    }

    public static async Task<List<AotRuntimeStatus?>> ProjectOptionalStatusAsync()
    {
        await using var context = new AotRuntimeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1")
            .Select(item => item.OptionalStatus)
            .ToListAsync();
    }

    public static async Task<(List<AotRuntimeItem> Items, string? ResponseNextToken)>
        LoadLimitedItemsAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-limit";
        var items = await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Limit(2)
            .ToListAsync();

        var responseNextToken = items.Count > 0
            ? context.Entry(items[0]).GetExecuteStatementResponse()?.NextToken
            : null;

        return (items, responseNextToken);
    }

    public static async Task<List<string>> LoadFirstPageAsync(int pageSizeArg)
    {
        await using var context = new AotRuntimeContext();
        var partitionKey = PagePartitionKey;
        var pageSize = pageSizeArg;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Limit(pageSize)
            .Select(item => item.Name)
            .ToListAsync();
    }

    public static async Task<List<string>> LoadNextPageAsync(int pageSizeArg, string nextTokenArg)
    {
        await using var context = new AotRuntimeContext();
        var partitionKey = PagePartitionKey;
        var pageSize = pageSizeArg;
        var nextToken = nextTokenArg;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Limit(pageSize)
            .WithNextToken(nextToken)
            .Select(item => item.Name)
            .ToListAsync();
    }

    public static async Task<AotRuntimeItem?> LoadByKeyAsync(
        string partitionKeyArg,
        string sortKeyArg)
    {
        await using var context = new AotRuntimeContext();
        var partitionKey = partitionKeyArg;
        var sortKey = sortKeyArg;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Sk == sortKey)
            .SingleOrDefaultAsync();
    }

    public static async Task<List<string>> LoadRuntimeItemNamesAsync()
    {
        await using var context = new AotRuntimeContext();
        return await context
            .Items
            .Where(item => item.Pk == "runtime-tenant")
            .Select(item => item.Name)
            .ToListAsync();
    }

    public static async Task<int> ExecuteDeleteAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "tenant-null";
        string sortKey = "sk-null";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Sk == sortKey)
            .ExecuteDeleteAsync();
    }
}
