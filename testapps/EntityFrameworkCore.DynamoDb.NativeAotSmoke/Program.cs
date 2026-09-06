using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

await using var server = FakeDynamoServer.Start();
Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "local");
Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "local");
Environment.SetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL", server.ServiceUrl);

var items = SmokeQueries.LoadItems();
if (items is not
    [
        {
            Pk: "tenant-1",
            Name: "Native",
            Status: SmokeStatus.Active,
            Count: 42,
            Enabled: true,
            Payload: [1, 2, 3],
            Tags: ["native"]
        }
    ])
    throw new InvalidOperationException("The generated query returned an unexpected result.");

Console.WriteLine("NativeAOT generated synchronous query executed successfully.");

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
}

public sealed class SmokeItem
{
    public string Pk { get; set; } = null!;
    public string Name { get; set; } = null!;
    public SmokeStatus Status { get; set; }
    public int? Count { get; set; }
    public bool Enabled { get; set; }
    public byte[] Payload { get; set; } = null!;
    public List<string> Tags { get; set; } = [];
}

public enum SmokeStatus
{
    Active
}

internal sealed class FakeDynamoServer : IAsyncDisposable
{
    private const string ExpectedStatement =
        "SELECT * FROM \"AotSmokeItems\" WHERE \"pk\" IN (?, ?)";

    private const string ResponseBody =
        "{\"Items\":[{\"pk\":{\"S\":\"tenant-1\"},\"$type\":{\"S\":\"SmokeItem\"},"
        + "\"name\":{\"S\":\"Native\"},\"status\":{\"S\":\"Active\"},"
        + "\"count\":{\"N\":\"42\"},\"enabled\":{\"BOOL\":true},\"payload\":{\"B\":\"AQID\"},"
        + "\"tags\":{\"L\":[{\"S\":\"native\"}]}}],"
        + "\"Count\":1,\"ScannedCount\":1}";

    private readonly TcpListener _listener;
    private readonly Task _requestTask;

    private FakeDynamoServer(TcpListener listener)
    {
        _listener = listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        ServiceUrl = $"http://127.0.0.1:{endpoint.Port}";
        _requestTask = HandleRequestAsync();
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
        if (_requestTask.IsCompleted)
        {
            await _requestTask;
            return;
        }

        _listener.Stop();
        try
        {
            await _requestTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (SocketException) { }
    }

    private async Task HandleRequestAsync()
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
        ValidateRequest(headers, requestBody);

        var body = Encoding.UTF8.GetBytes(ResponseBody);
        var responseHeaders = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
            + "Content-Type: application/x-amz-json-1.0\r\n"
            + $"Content-Length: {body.Length}\r\n"
            + "Connection: close\r\n\r\n");
        await stream.WriteAsync(responseHeaders);
        await stream.WriteAsync(body);
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

    private static void ValidateRequest(
        IReadOnlyDictionary<string, string> headers,
        string requestBody)
    {
        if (!headers.TryGetValue("X-Amz-Target", out var target)
            || target != "DynamoDB_20120810.ExecuteStatement")
            throw new InvalidOperationException("Expected an ExecuteStatement request.");

        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        AssertProperty(root, "Statement", ExpectedStatement);
        var parameters = root.GetProperty("Parameters");
        if (parameters.GetArrayLength() != 2)
            throw new InvalidOperationException(
                $"Expected two PartiQL parameters but received {parameters.GetArrayLength()}.");

        AssertProperty(parameters[0], "S", "tenant-1");
        AssertProperty(parameters[1], "S", "tenant-2");
    }

    private static void AssertProperty(
        JsonElement element,
        string propertyName,
        string expectedValue)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.GetString() != expectedValue)
            throw new InvalidOperationException(
                $"Expected request property '{propertyName}' to be '{expectedValue}'.");
    }
}
