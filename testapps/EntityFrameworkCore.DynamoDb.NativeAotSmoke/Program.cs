using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;

Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "local");
Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "local");

await SmokeDatabase.RecreateAsync();

await using (var context = new SmokeContext())
{
    context.AddRange(
        new SmokeItem
        {
            Pk = "tenant-1",
            Sk = "sk-1",
            Name = "Native",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Inactive,
            OptionalStatus = SmokeStatus.Active,
            Count = 42,
            Enabled = true,
            Payload = [1, 2, 3],
            Aliases = ["aot"]
        },
        new SmokeItem
        {
            Pk = "tenant-null",
            Sk = "sk-null",
            Name = "Nullable",
            Status = SmokeStatus.Inactive,
            RawStatus = SmokeStatus.Inactive,
            OptionalStatus = null,
            Count = null,
            Enabled = false,
            Payload = [0],
            Aliases = ["null"]
        },
        new SmokeItem
        {
            Pk = "tenant-limit",
            Sk = "sk-limit-1",
            Name = "Limit1",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 1,
            Enabled = true,
            Payload = [1],
            Aliases = ["limit"]
        },
        new SmokeItem
        {
            Pk = "tenant-limit",
            Sk = "sk-limit-2",
            Name = "Limit2",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 2,
            Enabled = true,
            Payload = [2],
            Aliases = ["limit"]
        },
        new SmokeItem
        {
            Pk = "tenant-limit",
            Sk = "sk-limit-3",
            Name = "Limit3",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 3,
            Enabled = true,
            Payload = [3],
            Aliases = ["limit"]
        },
        new SmokeItem
        {
            Pk = "tenant-limit",
            Sk = "sk-limit-4",
            Name = "Limit4",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 4,
            Enabled = true,
            Payload = [4],
            Aliases = ["limit"]
        },
        new SmokeItem
        {
            Pk = "tenant-page",
            Sk = "sk-page-1",
            Name = "Page1",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 1,
            Enabled = true,
            Payload = [1],
            Aliases = ["page"]
        },
        new SmokeItem
        {
            Pk = "tenant-page",
            Sk = "sk-page-2",
            Name = "Page2",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 2,
            Enabled = true,
            Payload = [2],
            Aliases = ["page"]
        },
        new SmokeItem
        {
            Pk = "tenant-page",
            Sk = "sk-page-3",
            Name = "Page3",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 3,
            Enabled = true,
            Payload = [3],
            Aliases = ["page"]
        },
        new SmokeItem
        {
            Pk = "tenant-page",
            Sk = "sk-page-4",
            Name = "Page4",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 4,
            Enabled = true,
            Payload = [4],
            Aliases = ["page"]
        },
        new SmokeItem
        {
            Pk = "tenant-page",
            Sk = "sk-page-5",
            Name = "Page5",
            Status = SmokeStatus.Active,
            RawStatus = SmokeStatus.Active,
            Count = 5,
            Enabled = true,
            Payload = [5],
            Aliases = ["page"]
        });
    await context.SaveChangesAsync();
}

var expectedItem = new SmokeItem
{
    Pk = "tenant-1",
    Sk = "sk-1",
    Name = "Native",
    Status = SmokeStatus.Active,
    RawStatus = SmokeStatus.Inactive,
    OptionalStatus = SmokeStatus.Active,
    Count = 42,
    Enabled = true,
    Payload = [1, 2, 3],
    Aliases = ["aot"]
};

var asyncItems = await SmokeQueries.LoadItemsAsync();
AssertSingleItem(asyncItems, expectedItem);
Console.WriteLine("NativeAOT generated asynchronous query executed successfully.");

var rawStatuses = await SmokeQueries.ProjectRawStatusAsync();
if (rawStatuses.Count != 1 || rawStatuses[0] != SmokeStatus.Inactive)
    throw new InvalidOperationException(
        "Expected the unconverted enum projection to materialize Inactive.");
var optionalStatuses = await SmokeQueries.ProjectOptionalStatusAsync();
if (optionalStatuses.Count != 1 || optionalStatuses[0] != SmokeStatus.Active)
    throw new InvalidOperationException(
        "Expected the nullable unconverted enum projection to materialize Active.");
Console.WriteLine("NativeAOT unconverted enum projections executed successfully.");

var activeItems = await SmokeQueries.LoadActiveItemsAsync();
AssertSingleItem(activeItems, expectedItem);
Console.WriteLine("NativeAOT converted-enum parameter query executed successfully.");

var countItems = await SmokeQueries.LoadItemsByCountAsync();
AssertSingleItem(countItems, expectedItem);
Console.WriteLine("NativeAOT numeric parameter query executed successfully.");

var sortKeyItems = await SmokeQueries.LoadItemsBySortKeyAsync();
AssertSingleItem(sortKeyItems, expectedItem);
Console.WriteLine("NativeAOT composite key predicate query executed successfully.");

var projectedStatus = await SmokeQueries.ProjectConvertedStatusAsync();
if (projectedStatus != SmokeStatus.Active)
    throw new InvalidOperationException(
        $"Expected converted projection 'Active' but received '{projectedStatus}'.");
Console.WriteLine("NativeAOT converted-scalar projection query executed successfully.");

var nullCountItems = await SmokeQueries.LoadItemsWithNullCountAsync();
AssertSingleItem(
    nullCountItems,
    new SmokeItem
    {
        Pk = "tenant-null",
        Sk = "sk-null",
        Name = "Nullable",
        Status = SmokeStatus.Inactive,
        RawStatus = SmokeStatus.Inactive,
        Count = null,
        Enabled = false,
        Payload = [0],
        Aliases = ["null"]
    });
Console.WriteLine("NativeAOT null-propagation query executed successfully.");

// Four items share the "tenant-limit" partition key; without a pushed-down Limit, the plain
// partition-key query below would evaluate and return all four. Asserting exactly 2 proves the
// DynamoDB evaluation budget was genuinely applied by the precompiled interceptor, not merely
// that the result set happened to be small.
var limitedItems = await SmokeQueries.LoadLimitedItemsAsync();
if (limitedItems.Count != 2)
    throw new InvalidOperationException(
        $"Expected Limit(2) to cap evaluated items at 2 but received {limitedItems.Count}.");
Console.WriteLine("NativeAOT Limit(n) evaluation-budget query executed successfully.");

// Realistic pagination composition across three real pages: `Limit(pageSize).WithNextToken
// (nextToken).ToListAsync()` is invoked with different runtime pageSize/nextToken values per
// page, proving the same generated interceptor is reused (not regenerated) and that continuation
// genuinely advances rather than restarting. This is the mechanics proof, not a recommended
// pagination API: `BootstrapNextTokenAsync` below is a validation-only technique (a raw AWS SDK
// call reading ExecuteStatementResponse.NextToken directly), used here solely to obtain a real
// token for the test, NOT a suggested application pattern. `ToPageAsync` remains the provider's
// real pagination API (it returns both items and NextToken together) but cannot run under
// NativeAOT today: EF Core's precompiler discovers query roots through a closed, upstream
// mechanism with no registration point for provider-defined terminals like ToPageAsync, so it is
// silently skipped and no interceptor is generated for it (tracked as an upstream EF Core
// limitation — see docs/limitations.md). Bootstrapping is not part of what this scenario proves —
// only LoadFirstPageAsync/LoadNextPageAsync (the precompiled Limit + WithNextToken path) is.
var page1 = await SmokeQueries.LoadFirstPageAsync(2);
if (page1.Count != 2)
    throw new InvalidOperationException(
        $"Expected pagination page 1 to contain 2 items but received {page1.Count}.");

var token1 = await SmokeQueries.BootstrapNextTokenAsync(2, null);
if (token1 is null)
    throw new InvalidOperationException("Expected a continuation token after pagination page 1.");

var page2 = await SmokeQueries.LoadNextPageAsync(2, token1);
if (page2.Count != 2)
    throw new InvalidOperationException(
        $"Expected pagination page 2 to contain 2 items but received {page2.Count}.");

var token2 = await SmokeQueries.BootstrapNextTokenAsync(2, token1);
if (token2 is null)
    throw new InvalidOperationException("Expected a continuation token after pagination page 2.");

var page3 = await SmokeQueries.LoadNextPageAsync(1, token2);
if (page3.Count != 1)
    throw new InvalidOperationException(
        $"Expected pagination page 3 to contain 1 item but received {page3.Count}.");

// DynamoDB may still return a LastEvaluatedKey when a Limit-bounded read happens to land exactly
// on the last item in the partition, even though nothing more exists beyond it — a non-null token
// here does not by itself mean more data remains. Confirm exhaustion the reliable way: page again
// and expect zero items back.
var token3 = await SmokeQueries.BootstrapNextTokenAsync(1, token2);
if (token3 is not null)
{
    var page4 = await SmokeQueries.LoadNextPageAsync(1, token3);
    if (page4.Count != 0)
        throw new InvalidOperationException(
            $"Expected pagination to be exhausted after pagination page 3 but page 4 returned {page4.Count} item(s).");
}

var allPageNames = page1.Concat(page2).Concat(page3).ToList();
var expectedPageNames = new[] { "Page1", "Page2", "Page3", "Page4", "Page5" };
if (allPageNames.Count != expectedPageNames.Length
    || allPageNames.Distinct().Count() != expectedPageNames.Length
    || !allPageNames.OrderBy(name => name, StringComparer.Ordinal)
        .SequenceEqual(expectedPageNames.OrderBy(name => name, StringComparer.Ordinal)))
    throw new InvalidOperationException(
        "Expected pagination across three pages to cover all five items exactly once without "
        + $"restarting, but got: {string.Join(", ", allPageNames)}.");
if (page1.Intersect(page2).Any() || page2.Intersect(page3).Any() || page1.Intersect(page3).Any())
    throw new InvalidOperationException("Expected no overlap between pagination pages.");

Console.WriteLine(
    "NativeAOT parameterized Limit + WithNextToken pagination executed successfully across 3 pages.");

var savedItem = new SmokeItem
{
    Pk = "tenant-9",
    Sk = "sk-9",
    Name = "Saved",
    Status = SmokeStatus.Inactive,
    Count = null,
    Enabled = false,
    Payload = [9],
    Aliases = ["write"]
};
await using (var context = new SmokeContext())
{
    context.Add(savedItem);
    await context.SaveChangesAsync();
}

await using (var context = new SmokeContext())
{
    var savedPk = savedItem.Pk;
    var savedSk = savedItem.Sk;
    var readBack =
        await context.Items.Where(item => item.Pk == savedPk && item.Sk == savedSk).SingleAsync();
    AssertSingleItem([readBack], savedItem);
}

Console.WriteLine("NativeAOT SaveChanges write and read-back executed successfully.");

static void AssertSingleItem(List<SmokeItem> items, SmokeItem expected)
{
    if (items.Count != 1)
        throw new InvalidOperationException($"Expected one item but received {items.Count}.");

    var actual = items[0];
    if (actual.Pk != expected.Pk
        || actual.Sk != expected.Sk
        || actual.Name != expected.Name
        || actual.Status != expected.Status
        || actual.RawStatus != expected.RawStatus
        || actual.OptionalStatus != expected.OptionalStatus
        || actual.Count != expected.Count
        || actual.Enabled != expected.Enabled
        || !actual.Payload.SequenceEqual(expected.Payload)
        || !actual.Aliases.SequenceEqual(expected.Aliases))
        throw new InvalidOperationException("The generated query returned an unexpected result.");
}

public sealed class SmokeContext : DbContext
{
    internal const string TableName = "AotSmokeItems";

    public DbSet<SmokeItem> Items => Set<SmokeItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(providerOptions
            => providerOptions.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL = Environment.GetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL")
                    ?? "http://127.0.0.1:9";
                config.AuthenticationRegion = "us-east-1";
            }));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SmokeItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, TableName);
            entity.HasPartitionKey(item => item.Pk);
            DynamoEntityTypeBuilderExtensions.HasSortKey(entity, item => item.Sk);
            entity.Property(item => item.Status).HasConversion<string>();
        });
}

internal static class SmokeDatabase
{
    public static async Task RecreateAsync()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL")
            ?? throw new InvalidOperationException("DYNAMO_AOT_SMOKE_URL is required.");
        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("local", "local"),
            new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1"
            });

        try
        {
            await client.DeleteTableAsync(SmokeContext.TableName);
            while (true)
                try
                {
                    await client.DescribeTableAsync(SmokeContext.TableName);
                    await Task.Delay(100);
                }
                catch (ResourceNotFoundException)
                {
                    break;
                }
        }
        catch (ResourceNotFoundException) { }

        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SmokeContext.TableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition("pk", ScalarAttributeType.S),
                    new AttributeDefinition("sk", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("pk", KeyType.HASH),
                    new KeySchemaElement("sk", KeyType.RANGE)
                ]
            });

        while ((await client.DescribeTableAsync(SmokeContext.TableName)).Table.TableStatus
            != TableStatus.ACTIVE)
            await Task.Delay(100);
    }
}

internal static class SmokeQueries
{
    private const string PagePartitionKey = "tenant-page";

    internal static async Task<List<SmokeItem>> LoadItemsAsync()
    {
        await using var context = new SmokeContext();
        string[] partitionKeys = ["tenant-1", "tenant-2"];
        return await context
            .Items
            .Where(item => ((IEnumerable<string>)partitionKeys).Contains(item.Pk))
            .ToListAsync();
    }

    internal static async Task<List<SmokeItem>> LoadActiveItemsAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-1";
        var active = SmokeStatus.Active;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Status == active)
            .ToListAsync();
    }

    internal static async Task<List<SmokeItem>> LoadItemsByCountAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-1";
        int count = 42;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Count == count)
            .ToListAsync();
    }

    internal static async Task<List<SmokeItem>> LoadItemsBySortKeyAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-1";
        string sortKey = "sk-1";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Sk == sortKey)
            .ToListAsync();
    }

    internal static async Task<SmokeStatus> ProjectConvertedStatusAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-1";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Select(item => item.Status)
            .FirstAsync();
    }

    internal static async Task<List<SmokeItem>> LoadItemsWithNullCountAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-null";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey && item.Count == null)
            .ToListAsync();
    }

    internal static async Task<List<SmokeStatus>> ProjectRawStatusAsync()
    {
        await using var context = new SmokeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1")
            .Select(item => item.RawStatus)
            .ToListAsync();
    }

    internal static async Task<List<SmokeStatus?>> ProjectOptionalStatusAsync()
    {
        await using var context = new SmokeContext();
        return await context
            .Items
            .Where(item => item.Pk == "tenant-1")
            .Select(item => item.OptionalStatus)
            .ToListAsync();
    }

    internal static async Task<List<SmokeItem>> LoadLimitedItemsAsync()
    {
        await using var context = new SmokeContext();
        string partitionKey = "tenant-limit";
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Limit(2)
            .ToListAsync();
    }

    internal static async Task<List<string>> LoadFirstPageAsync(int pageSizeArg)
    {
        await using var context = new SmokeContext();
        var partitionKey = PagePartitionKey;
        var pageSize = pageSizeArg;
        return await context
            .Items
            .Where(item => item.Pk == partitionKey)
            .Limit(pageSize)
            .Select(item => item.Name)
            .ToListAsync();
    }

    internal static async Task<List<string>> LoadNextPageAsync(int pageSizeArg, string nextTokenArg)
    {
        await using var context = new SmokeContext();
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

    // TEST-ONLY validation helper — not a recommended application pattern. Not part of the
    // precompiled/AOT-critical path this scenario proves. EF Core's precompiler does not
    // currently recognize ToPageAsync as a query root: root discovery is a closed, upstream
    // mechanism with no registration point for provider-defined terminals (a tracked, upstream EF
    // Core limitation — see docs/limitations.md). Unlike other non-precompiled query shapes, a
    // query with no generated interceptor at all does not fall back to interpreted execution under
    // NativeAOT: it throws ("Query wasn't precompiled and dynamic code isn't supported with
    // NativeAOT"), confirmed empirically in this smoke app. This method bootstraps a real
    // continuation-token value via a raw AWS SDK ExecuteStatement call purely so the test below can
    // observe real pagination behavior — the exact mechanism DynamoClientWrapper itself uses under
    // the hood (ExecuteStatementResponse.NextToken flows through unmodified into WithNextToken(...)
    // and DynamoPage.NextToken; see DynamoClientWrapper.cs). DynamoDB's LastEvaluatedKey/NextToken
    // is a function of table, key condition, Limit, and ExclusiveStartKey — not of the PartiQL
    // projection list — so this key-only statement produces the same continuation position as the
    // precompiled query under test.
    internal static async Task<string?> BootstrapNextTokenAsync(int pageSize, string? seedToken)
    {
        var serviceUrl = Environment.GetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL")
            ?? throw new InvalidOperationException("DYNAMO_AOT_SMOKE_URL is required.");
        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("local", "local"),
            new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1"
            });

        var response = await client.ExecuteStatementAsync(
            new ExecuteStatementRequest
            {
                Statement = $"SELECT \"pk\", \"sk\" FROM \"{SmokeContext.TableName}\" WHERE \"pk\" = ?",
                Parameters = [new AttributeValue { S = PagePartitionKey }],
                Limit = pageSize,
                NextToken = seedToken
            });

        return response.NextToken;
    }
}

public sealed class SmokeItem
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Name { get; set; } = null!;
    public SmokeStatus Status { get; set; }
    public SmokeStatus RawStatus { get; set; }
    public SmokeStatus? OptionalStatus { get; set; }
    public int? Count { get; set; }
    public bool Enabled { get; set; }
    public byte[] Payload { get; set; } = null!;
    public string[] Aliases { get; set; } = [];
}

public enum SmokeStatus
{
    Active,
    Inactive
}
