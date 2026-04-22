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

### Injecting a pre-built client

Use `DynamoDbClient` when you need full control over the AWS SDK client — for example, to attach a
custom retry policy, share a client across multiple contexts, or inject a mock in tests:

```csharp
var dynamoClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
});

services.AddDbContext<ShopContext>(options =>
    options.UseDynamo(o => o.DynamoDbClient(dynamoClient)));
```

### Inline callback configuration

`ConfigureDynamoDbClientConfig` is the lightest option — you receive an `AmazonDynamoDBConfig`
and mutate it before the provider creates the client:

```csharp
optionsBuilder.UseDynamo(options =>
    options.ConfigureDynamoDbClientConfig(config =>
    {
        config.RegionEndpoint = RegionEndpoint.EUWest1;
    }));
```

### Supplying a base config object

`DynamoDbClientConfig` accepts a fully constructed `AmazonDynamoDBConfig`. Use this when the
config object is built elsewhere (for example, from application settings):

```csharp
var sdkConfig = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.APSoutheast1,
};

optionsBuilder.UseDynamo(o => o.DynamoDbClientConfig(sdkConfig));
```

### Retrieving the resolved client

After the context is configured you can retrieve the `IAmazonDynamoDB` instance the provider is
using. This is useful for raw SDK calls or test assertions without having to keep a separate
reference:

```csharp
var client = context.Database.GetDynamoClient();
```

## Using DynamoDB Local

[DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
runs an in-process DynamoDB server on your machine — useful for local development and integration
tests without hitting AWS.

Point the provider at it by setting `ServiceURL` and disabling HTTPS:

```csharp
optionsBuilder.UseDynamo(options =>
    options.ConfigureDynamoDbClientConfig(config =>
    {
        config.ServiceURL = "http://localhost:8000";
        config.AuthenticationRegion = "us-east-1";
        config.UseHttp = true;
    }));
```

!!! tip "Credentials for DynamoDB Local"

    DynamoDB Local does not validate credentials. Any non-empty string works. If the standard
    credential chain cannot resolve real AWS credentials in your environment, set dummy values via
    environment variables:

    ```bash
    AWS_ACCESS_KEY_ID=local
    AWS_SECRET_ACCESS_KEY=local
    ```

If you want to share the same `AmazonDynamoDBClient` across multiple test contexts, inject it via
`DynamoDbClient` instead:

```csharp
var localClient = new AmazonDynamoDBClient(
    new BasicAWSCredentials("local", "local"),
    new AmazonDynamoDBConfig
    {
        ServiceURL = "http://localhost:8000",
        UseHttp = true,
    });

optionsBuilder.UseDynamo(o => o.DynamoDbClient(localClient));
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

To use explicit credentials — for example, from a secrets store or injected at runtime:

```csharp
var credentials = new BasicAWSCredentials(accessKey, secretKey);
var client = new AmazonDynamoDBClient(credentials, new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
});

optionsBuilder.UseDynamo(o => o.DynamoDbClient(client));
```

## See also

- [DbContext Options](dbcontext.md)
- [Getting Started](../getting-started.md)
