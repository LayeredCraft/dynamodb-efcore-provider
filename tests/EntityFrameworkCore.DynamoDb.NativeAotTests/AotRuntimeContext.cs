using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.NativeAotTests;

public sealed class AotRuntimeContext : DbContext
{
    // The compiled model and the precompiled query interceptors are generated with these design-time
    // physical names. The physical names below are supplied at runtime through the supported
    // provider API, so the same compiled artifact is configured without regenerating anything.
    internal const string LogicalTableName = "AotRuntimeItems";
    internal const string LogicalIndexName = "ByName";
    internal const string DesignTimeTableName = "AotRuntimeItems";
    internal const string DesignTimeIndexName = "AotRuntimeItems-name-design";
    internal const string RuntimeTableName = "AotRuntimeConfiguredItems-runtime";
    internal const string RuntimeIndexName = "AotRuntimeConfiguredItems-name-runtime";

    internal const string QuestionsTableName = "AotRuntimeQuestions";

    /// <summary>The physical table every test in this process runs against.</summary>
    internal const string TableName = RuntimeTableName;

    public DbSet<AotRuntimeItem> Items => Set<AotRuntimeItem>();

    public DbSet<AotRuntimeQuestion> Questions => Set<AotRuntimeQuestion>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(providerOptions =>
        {
            providerOptions.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL =
                    Environment.GetEnvironmentVariable(DynamoFixture.ServiceUrlEnvironmentVariable)
                    ?? "http://127.0.0.1:9";
                config.AuthenticationRegion = "us-east-1";
            });

            // Model and query-interceptor generation must see the design-time names only, so the
            // runtime mapping is applied to the running application, never to the generator.
            if (!EF.IsDesignTime)
                providerOptions.RuntimeResourceNames(names => names
                    .Table(LogicalTableName, RuntimeTableName)
                    .SecondaryIndex(LogicalTableName, LogicalIndexName, RuntimeIndexName));
        });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Default property-access mode on purpose: precompiled complex-collection materialization
        // must work without UsePropertyAccessMode(PreferProperty).
        modelBuilder.Entity<AotRuntimeQuestion>(entity =>
        {
            // Its own table: sharing the items table would add a discriminator filter to every
            // items query. Not part of the runtime resource-name mapping.
            DynamoEntityTypeBuilderExtensions.ToTable(entity, QuestionsTableName);
            entity.HasPartitionKey(question => question.Pk);
            DynamoEntityTypeBuilderExtensions.HasSortKey(entity, question => question.Sk);
            entity.ComplexProperty(question => question.Details);
            entity.ComplexCollection(question => question.Answers);
        });

        modelBuilder.Entity<AotRuntimeItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions
                .ToTable(entity, DesignTimeTableName)
                .HasLogicalTableName(LogicalTableName);
            entity.HasPartitionKey(item => item.Pk);
            DynamoEntityTypeBuilderExtensions.HasSortKey(entity, item => item.Sk);
            entity
                .HasGlobalSecondaryIndex(LogicalIndexName, nameof(AotRuntimeItem.Name))
                .HasSecondaryIndexName(DesignTimeIndexName);
            entity.Property(item => item.Status).HasConversion<string>();
        });
    }
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

    public static async Task<List<AotRuntimeQuestion>> LoadQuestionsAsync()
    {
        await using var context = new AotRuntimeContext();
        string partitionKey = "question-tenant";

        // No-tracking: EF Core's compiled model does not yet include the value factories the change
        // tracker needs for complex collections under NativeAOT (dotnet/efcore#37750).
        return await context
            .Questions
            .AsNoTracking()
            .Where(question => question.Pk == partitionKey)
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

    public static async Task<List<AotRuntimeItem>> LoadByNameAsync(string nameArg)
    {
        await using var context = new AotRuntimeContext();
        var name = nameArg;
        return await context.Items.Where(item => item.Name == name).ToListAsync();
    }

    public static async Task<List<AotRuntimeItem>> LoadByNameWithIndexHintAsync(string nameArg)
    {
        await using var context = new AotRuntimeContext();
        var name = nameArg;
        // The hint is translated against the design-time model, so it names the design-time physical
        // index (the literal below equals AotRuntimeContext.DesignTimeIndexName); at runtime the
        // precompiled template resolves it to the runtime physical index.
        return await context
            .Items
            .WithIndex("AotRuntimeItems-name-design")
            .Where(item => item.Name == name)
            .ToListAsync();
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

    public static async Task<int> ExecuteUpdateNameAsync()
    {
        await using var context = new AotRuntimeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1" && item.Sk == "sk-1")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Name, "Updated"));
    }

    public static async Task<int> ExecuteUpdateCountAsync()
    {
        await using var context = new AotRuntimeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1" && item.Sk == "sk-1")
            .ExecuteUpdateAsync(setters
                => setters.SetProperty(item => item.Count, item => item.Count + 1));
    }
}
