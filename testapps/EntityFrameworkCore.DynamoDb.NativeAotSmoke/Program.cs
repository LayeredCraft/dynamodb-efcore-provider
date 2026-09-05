using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;

await using var server = FakeDynamoServer.Start();
Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "local");
Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "local");
Environment.SetEnvironmentVariable("DYNAMO_AOT_SMOKE_URL", server.ServiceUrl);

var items = await SmokeQueries.LoadItemsAsync();
if (items is not [{ Pk: "tenant-1", Name: "Native", Status: SmokeStatus.Active }])
    throw new InvalidOperationException("The generated query returned an unexpected result.");

Console.WriteLine("NativeAOT generated query executed successfully.");

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
}

public enum SmokeStatus
{
    Active
}

internal sealed class FakeDynamoServer : IAsyncDisposable
{
    private const string ResponseBody =
        "{\"Items\":[{\"pk\":{\"S\":\"tenant-1\"},\"$type\":{\"S\":\"SmokeItem\"},"
        + "\"name\":{\"S\":\"Native\"},\"status\":{\"S\":\"Active\"}}],"
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
            await _requestTask;
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

        while (await reader.ReadLineAsync() is { Length: > 0 }) { }

        var body = Encoding.UTF8.GetBytes(ResponseBody);
        var headers = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
            + "Content-Type: application/x-amz-json-1.0\r\n"
            + $"Content-Length: {body.Length}\r\n"
            + "Connection: close\r\n\r\n");
        await stream.WriteAsync(headers);
        await stream.WriteAsync(body);
    }
}
