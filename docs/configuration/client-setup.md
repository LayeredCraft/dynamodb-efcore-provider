---
title: Client Setup
description: How to configure the DynamoDB client used by the provider.
---

# Client Setup

_The provider communicates with DynamoDB through the AWS SDK's `IAmazonDynamoDB` interface. You
control which client it uses — and how that client is configured — through the `UseDynamo` options
builder._

## Configuring the DynamoDB Client

The options builder exposes three methods for client configuration. They are mutually usable and
resolved in priority order at startup:

| Priority    | Method                                                        | When to use                                                                        |
| ----------- | ------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| 1 (highest) | `DynamoDbClient(IAmazonDynamoDB)`                             | Inject a fully configured client — custom retry policy, test mock, shared instance |
| 2           | `DynamoDbClientConfig(AmazonDynamoDBConfig)`                  | Supply a base SDK config object; the provider creates the client                   |
| 3 (lowest)  | `ConfigureDynamoDbClientConfig(Action<AmazonDynamoDBConfig>)` | Mutate config inline without constructing objects yourself                         |

When `DynamoDbClient` is set, the other two options are ignored — the provider uses the injected
client as-is.

!!! note

    These options are set inside the `UseDynamo` callback. The callback itself works identically
    whether you call `UseDynamo` from `OnConfiguring` or from `AddDbContext` — the choice of
    registration style does not affect which client method you use. See
    [DbContext Options](dbcontext.md) if you are deciding between those two approaches.

### Inline callback configuration

`ConfigureDynamoDbClientConfig` is the lightest option — the provider creates the client for you
after you configure the `AmazonDynamoDBConfig`:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(options =>
        options.ConfigureDynamoDbClientConfig(config =>
        {
            config.RegionEndpoint = RegionEndpoint.EUWest1;
        }));
```

Use this for simple cases where you only need to set a region or endpoint and do not need to share
the client with other services.

### Supplying a base config object

`DynamoDbClientConfig` accepts a fully constructed `AmazonDynamoDBConfig`. Use this when the
config object is built elsewhere — for example, deserialized from application settings — and you
want the provider to create the client from it:

```csharp
var sdkConfig = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.APSoutheast1,
};

protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(o => o.DynamoDbClientConfig(sdkConfig));
```

### Injecting a pre-built client

Use `DynamoDbClient` when you need full control over the client — attaching a custom retry policy,
sharing a single instance across multiple contexts, or swapping in a mock during tests:

```csharp
var dynamoClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
});

protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(o => o.DynamoDbClient(dynamoClient));
```

In an ASP.NET Core app, you typically register `IAmazonDynamoDB` in the DI container and pass it
through `AddDbContext`, so both the context and other services share the same client instance:

```csharp
// Program.cs
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
}));

builder.Services.AddDbContext<ShopContext>((serviceProvider, options) =>
{
    var client = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
    options.UseDynamo(o => o.DynamoDbClient(client));
});
```

### Retrieving the resolved client

After the context is configured you can retrieve the `IAmazonDynamoDB` instance the provider is
using — useful for raw SDK calls or test assertions without keeping a separate reference:

```csharp
var client = context.Database.GetDynamoClient();
```

## Using DynamoDB Local

[DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
runs a DynamoDB-compatible server on your machine, useful for local development and integration
tests without hitting AWS.

Point the provider at it by setting `ServiceURL` and disabling HTTPS:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(options =>
        options.ConfigureDynamoDbClientConfig(config =>
        {
            config.ServiceURL = "http://localhost:8000";
            config.AuthenticationRegion = "us-east-1";
            config.UseHttp = true;
        }));
```

!!! tip "Credentials for DynamoDB Local"

    DynamoDB Local does not validate credentials — any non-empty string works. You have two options:

    **Option 1 — environment variables.** Set these before running your app or tests and the
    standard AWS credential chain will pick them up automatically:

    ```bash
    AWS_ACCESS_KEY_ID=local
    AWS_SECRET_ACCESS_KEY=local
    ```

    **Option 2 — pass credentials explicitly via `BasicAWSCredentials`.** Use this when you cannot
    or do not want to set environment variables — for example, in a test fixture that manages its
    own client:

    ```csharp
    new AmazonDynamoDBClient(
        new BasicAWSCredentials("test", "test"),
        new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
    ```

When multiple test contexts need to share a single connection — for example, in a test fixture that
resets tables between tests — register a shared client via `DynamoDbClient`:

```csharp
var localClient = new AmazonDynamoDBClient(
    new BasicAWSCredentials("local", "local"),
    new AmazonDynamoDBConfig
    {
        ServiceURL = "http://localhost:8000",
        UseHttp = true,
    });

protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(o => o.DynamoDbClient(localClient));
```

## Authentication and Region

The provider does not implement its own authentication. All credential resolution goes through the
standard [AWS credential chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html):

1. Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`)
1. `~/.aws/credentials` file
1. IAM instance profile or ECS task role (when running on AWS)

To specify a region without constructing a full config object:

```csharp
options.ConfigureDynamoDbClientConfig(config =>
    config.RegionEndpoint = RegionEndpoint.USWest2);
```

To use explicit credentials — for example, read from a secrets store at startup:

```csharp
var credentials = new BasicAWSCredentials(accessKey, secretKey);
var dynamoClient = new AmazonDynamoDBClient(credentials, new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
});

optionsBuilder.UseDynamo(o => o.DynamoDbClient(dynamoClient));
```

## See also

- [DbContext Options](dbcontext.md)
- [Getting Started](../getting-started.md)
