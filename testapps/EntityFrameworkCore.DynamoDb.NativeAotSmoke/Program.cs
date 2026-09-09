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
