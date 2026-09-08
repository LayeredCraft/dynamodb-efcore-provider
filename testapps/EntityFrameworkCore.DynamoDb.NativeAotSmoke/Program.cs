using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

await using (var server = FakeDynamoServer.Start())
{
    Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "local");
    Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "local");
    Environment.SetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL", server.ServiceUrl);

    var expectedItem = new SmokeItem
    {
        Pk = "tenant-1",
        Name = "Native",
        Status = SmokeStatus.Active,
        Count = 42,
        Enabled = true,
        Payload = [1, 2, 3],
        Aliases = ["aot"]
    };

    var items = SmokeQueries.LoadItems();
    AssertSingleItem(items, expectedItem);
    Console.WriteLine("NativeAOT generated synchronous query executed successfully.");
    Console.Out.Flush();

    var asyncItems = await SmokeQueries.LoadItemsAsync();
    AssertSingleItem(asyncItems, expectedItem);
    Console.WriteLine("NativeAOT generated asynchronous query executed successfully.");
    Console.Out.Flush();

    var activeItems = await SmokeQueries.LoadActiveItemsAsync();
    AssertSingleItem(activeItems, expectedItem);
    Console.WriteLine("NativeAOT converted-enum parameter query executed successfully.");
    Console.Out.Flush();

    var countItems = await SmokeQueries.LoadItemsByCountAsync();
    AssertSingleItem(countItems, expectedItem);
    Console.WriteLine("NativeAOT numeric parameter query executed successfully.");
    Console.Out.Flush();

    await using (var context = new SmokeContext())
    {
        context.Add(
            new SmokeItem
            {
                Pk = "tenant-9",
                Name = "Saved",
                Status = SmokeStatus.Inactive,
                Count = null,
                Enabled = false,
                Payload = [9],
                Aliases = ["write"]
            });
        await context.SaveChangesAsync();
    }

    Console.WriteLine("NativeAOT SaveChanges write executed successfully.");
    Console.Out.Flush();
}

static void AssertSingleItem(List<SmokeItem> items, SmokeItem expected)
{
    if (items.Count != 1)
        throw new InvalidOperationException($"Expected one item but received {items.Count}.");

    var actual = items[0];
    if (actual.Pk != expected.Pk
        || actual.Name != expected.Name
        || actual.Status != expected.Status
        || actual.Count != expected.Count
        || actual.Enabled != expected.Enabled
        || !actual.Payload.SequenceEqual(expected.Payload)
        || !actual.Aliases.SequenceEqual(expected.Aliases))
        throw new InvalidOperationException("The generated query returned an unexpected result.");
}

public sealed class SmokeContext : DbContext
{
    public DbSet<SmokeItem> Items => Set<SmokeItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(providerOptions
            => providerOptions.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL =
                    Environment.GetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL")
                    ?? "http://127.0.0.1:9";
                config.AuthenticationRegion = "us-east-1";
            }));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SmokeItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, "AotSmokeItems");
            entity.HasPartitionKey(item => item.Pk);
            entity.Property(item => item.Status).HasConversion<string>();
        });
}

internal static class SmokeQueries
{
    internal static List<SmokeItem> LoadItems()
    {
        using var context = new SmokeContext();
        string[] partitionKeys = ["tenant-1", "tenant-2"];
        return context
            .Items
            .Where(item => ((IEnumerable<string>)partitionKeys).Contains(item.Pk))
            .ToList();
    }

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
}

public sealed class SmokeItem
{
    public string Pk { get; set; } = null!;
    public string Name { get; set; } = null!;
    public SmokeStatus Status { get; set; }
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

internal sealed class FakeDynamoServer : IAsyncDisposable
{
    private const string SelectPrefix = "SELECT \"pk\", \"$type\", \"aliases\", \"count\", "
        + "\"enabled\", \"name\", \"payload\", \"status\"\nFROM \"AotSmokeItems\"";

    private const string ResponseBody =
        "{\"Items\":[{\"pk\":{\"S\":\"tenant-1\"},\"$type\":{\"S\":\"SmokeItem\"},"
        + "\"name\":{\"S\":\"Native\"},\"status\":{\"S\":\"Active\"},"
        + "\"count\":{\"N\":\"42\"},\"enabled\":{\"BOOL\":true},\"payload\":{\"B\":\"AQID\"},"
        + "\"aliases\":{\"L\":[{\"S\":\"aot\"}]}}],"
        + "\"Count\":1,\"ScannedCount\":1}";

    private const string WriteResponseBody = "{\"Items\":[],\"Count\":0,\"ScannedCount\":0}";

    private readonly TcpListener _listener;
    private readonly Task _requestTask;
    private int _servedRequests;

    private FakeDynamoServer(TcpListener listener)
    {
        _listener = listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        ServiceUrl = $"http://127.0.0.1:{endpoint.Port}";
        _requestTask = ServeRequestsAsync();
    }

    public string ServiceUrl { get; }

    public static FakeDynamoServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new FakeDynamoServer(listener);
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        try
        {
            await _requestTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception exception) when (
            exception is SocketException or OperationCanceledException)
        {
            // The listener stop cancels a pending accept after the final served request.
        }
    }

    private const int ExpectedRequestCount = 5;

    private async Task ServeRequestsAsync()
    {
        while (_servedRequests < ExpectedRequestCount)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var headers = await ReadHeadersAsync(reader);
            var requestBody = await ReadBodyAsync(reader, headers);
            var isWrite = ValidateRequest(headers, requestBody, _servedRequests++);

            var body = Encoding.UTF8.GetBytes(isWrite ? WriteResponseBody : ResponseBody);
            var responseHeaders = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: application/x-amz-json-1.0\r\n"
                + $"Content-Length: {body.Length}\r\n"
                + "Connection: close\r\n\r\n");
            await stream.WriteAsync(responseHeaders);
            await stream.WriteAsync(body);
        }
    }

    private static async Task<Dictionary<string, string>> ReadHeadersAsync(StreamReader reader)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requestLine = await reader.ReadLineAsync()
            ?? throw new InvalidOperationException(
                "The DynamoDB request did not include a request line.");
        if (!requestLine.StartsWith("POST ", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Expected a POST request but received '{requestLine}'.");

        while (await reader.ReadLineAsync() is { Length: > 0 } line)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                throw new InvalidOperationException($"Invalid HTTP header '{line}'.");

            headers.Add(line[..separator], line[(separator + 1)..].Trim());
        }

        return headers;
    }

    private static async Task<string> ReadBodyAsync(
        StreamReader reader,
        IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Content-Length", out var contentLengthText)
            || !int.TryParse(contentLengthText, out var contentLength)
            || contentLength < 0)
            throw new InvalidOperationException(
                "The DynamoDB request did not include a valid Content-Length header.");

        var body = new char[contentLength];
        for (var offset = 0; offset < body.Length;)
        {
            var read = await reader.ReadBlockAsync(body, offset, body.Length - offset);
            if (read == 0)
                throw new InvalidOperationException(
                    "The DynamoDB request body ended before Content-Length bytes were received.");

            offset += read;
        }

        return new string(body);
    }

    private static bool ValidateRequest(
        IReadOnlyDictionary<string, string> headers,
        string requestBody,
        int requestIndex)
    {
        if (!headers.TryGetValue("X-Amz-Target", out var target)
            || target != "DynamoDB_20120810.ExecuteStatement")
            throw new InvalidOperationException(
                $"Expected an ExecuteStatement request but received '{target}'.");

        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        var statement = root.GetProperty("Statement").GetString() ?? string.Empty;

        if (statement.StartsWith("INSERT", StringComparison.Ordinal))
        {
            ValidateWriteRequest(root, statement);
            return true;
        }

        if (requestIndex is 0 or 1)
            ValidatePartitionKeyRequest(root, statement);
        else if (requestIndex == 2)
            ValidateStatusRequest(root, statement);
        else if (requestIndex == 3)
            ValidateCountRequest(root, statement);
        else
            throw new InvalidOperationException(
                $"Received more read requests ({requestIndex + 1}) than expected.");

        return false;
    }

    private static void ValidatePartitionKeyRequest(JsonElement root, string statement)
    {
        AssertStatement(statement, SelectPrefix + "\nWHERE \"pk\" IN [?, ?]");

        var parameters = root.GetProperty("Parameters");
        if (parameters.GetArrayLength() != 2)
            throw new InvalidOperationException(
                $"Expected two PartiQL parameters but received {parameters.GetArrayLength()}.");

        AssertProperty(parameters[0], "S", "tenant-1");
        AssertProperty(parameters[1], "S", "tenant-2");
    }

    private static void ValidateStatusRequest(JsonElement root, string statement)
    {
        AssertStatement(statement, SelectPrefix + "\nWHERE \"pk\" = ? AND \"status\" = ?");

        var parameters = root.GetProperty("Parameters");
        if (parameters.GetArrayLength() != 2)
            throw new InvalidOperationException(
                $"Expected two PartiQL parameters but received {parameters.GetArrayLength()}.");

        AssertProperty(parameters[0], "S", "tenant-1");
        AssertProperty(parameters[1], "S", nameof(SmokeStatus.Active));
    }

    private static void ValidateCountRequest(JsonElement root, string statement)
    {
        AssertStatement(statement, SelectPrefix + "\nWHERE \"pk\" = ? AND \"count\" = ?");

        var parameters = root.GetProperty("Parameters");
        if (parameters.GetArrayLength() != 2)
            throw new InvalidOperationException(
                $"Expected two PartiQL parameters but received {parameters.GetArrayLength()}.");

        AssertProperty(parameters[0], "S", "tenant-1");
        AssertProperty(parameters[1], "N", "42");
    }

    private static void ValidateWriteRequest(JsonElement root, string statement)
    {
        if (!statement.StartsWith("INSERT INTO \"AotSmokeItems\"", StringComparison.Ordinal)
            || !statement.Contains("'pk': ?", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Unexpected INSERT statement shape: '{statement}'.");

        var parameters = root.GetProperty("Parameters");
        if (parameters.GetArrayLength() < 8)
            throw new InvalidOperationException(
                $"Expected write parameters but received {parameters.GetArrayLength()}.");

        var hasPartitionKey = false;
        var hasStatus = false;
        var hasNullCount = false;
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.TryGetProperty("S", out var stringValue)
                && stringValue.GetString() == "tenant-9")
                hasPartitionKey = true;
            if (parameter.TryGetProperty("S", out stringValue)
                && stringValue.GetString() == nameof(SmokeStatus.Inactive))
                hasStatus = true;
            if (parameter.TryGetProperty("NULL", out var nullValue) && nullValue.GetBoolean())
                hasNullCount = true;
        }

        if (!hasPartitionKey || !hasStatus || !hasNullCount)
            throw new InvalidOperationException(
                "The SaveChanges write did not serialize the expected wire values "
                + $"(pk={hasPartitionKey}, status={hasStatus}, nullCount={hasNullCount}).");
    }

    private static void AssertStatement(string statement, string expected)
    {
        if (statement != expected)
            throw new InvalidOperationException(
                $"Expected statement '{expected}' but received '{statement}'.");
    }

    private static void AssertProperty(
        JsonElement element,
        string propertyName,
        string expectedValue)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.GetString() is not { } actualValue
            || actualValue != expectedValue)
            throw new InvalidOperationException(
                $"Expected request property '{propertyName}' to be '{expectedValue}', but received "
                + $"'{property.GetString() ?? "<missing>"}'.");
    }
}
