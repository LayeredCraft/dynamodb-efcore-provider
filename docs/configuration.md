---
icon: lucide/settings
---

# Configuration

## DbContext setup
- Configure the provider with `UseDynamo()` or `UseDynamo(options => ...)`.
- For local development and integration tests, pass a configured `IAmazonDynamoDB` client.

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.ConfigureDynamoDbClientConfig(config =>
    {
        config.ServiceURL = "http://localhost:8000";
        config.AuthenticationRegion = "us-east-1";
        config.UseHttp = true;
    });
    options.DefaultPageSize(100);
});
```

## Options
- `DefaultPageSize`: default request page size (`ExecuteStatementRequest.Limit`) for queries when no per-query override is present.
- `DynamoDbClient`: use a preconfigured `IAmazonDynamoDB` instance.
- `DynamoDbClientConfig`: use a preconfigured `AmazonDynamoDBConfig` when creating the SDK client.
- `ConfigureDynamoDbClientConfig`: apply a callback to configure `AmazonDynamoDBConfig` before client creation.
- `DefaultPageSize` must be greater than zero.

## Client configuration precedence
- The provider resolves client settings in this order:
  1. `DynamoDbClient(...)` (explicit client instance)
  2. `DynamoDbClientConfig(...)` (base SDK config)
  3. `ConfigureDynamoDbClientConfig(...)` (callback adjustments)

```csharp
var sharedClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    ServiceURL = "http://localhost:8000",
    AuthenticationRegion = "us-east-1",
});

optionsBuilder.UseDynamo(options =>
{
    options.DynamoDbClient(sharedClient);
});
```

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.ConfigureDynamoDbClientConfig(config =>
    {
        config.ServiceURL = "http://localhost:7001";
        config.AuthenticationRegion = "us-west-2";
        config.UseHttp = true;
    });
});
```

## Table mapping
- Use `ToTable("TableName")` to map an entity to a DynamoDB table.
- If omitted, the provider falls back to the entity CLR type name.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SimpleItem>()
        .ToTable("SimpleItems")
        .HasKey(x => x.Pk);
}
```

## Key configuration

DynamoDB tables have a partition key and an optional sort key. The provider needs to know which
EF properties map to those keys so it can build correct key expressions.

### Convention-based discovery

Properties named `PK` or `PartitionKey` are automatically designated as the DynamoDB partition
key. Properties named `SK` or `SortKey` are automatically designated as the sort key. The
comparison is case-insensitive (ordinal ignore-case).

```csharp
public class Order
{
    public string PK { get; set; }   // auto-discovered as partition key
    public string SK { get; set; }   // auto-discovered as sort key
    public string Description { get; set; }
}
```

When the convention fires, the EF primary key is automatically set to `[PK]` or `[PK, SK]` —
no explicit `HasKey` call is needed.

If a type has both `PK` and `PartitionKey` properties (or both `SK` and `SortKey`) and no explicit
override is configured, the provider throws `InvalidOperationException` during model finalization.
Use `HasPartitionKey` or `HasSortKey` to resolve the ambiguity.
This ambiguity check uses the same case-insensitive matching as key discovery.

### Explicit configuration

Use `HasPartitionKey` and `HasSortKey` to designate key properties explicitly. This is required
when the key property names do not follow the conventions above, and also overrides convention
discovery when both are present.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);
    // EF primary key is automatically set to [CustomerId, OrderId]
});
```

When `HasPartitionKey` and/or `HasSortKey` are set without an explicit `HasKey` call, the provider
automatically configures the EF primary key to match. An explicit `HasKey` call always takes
precedence over convention.

### String-based overload

```csharp
b.HasPartitionKey("CustomerId");
b.HasSortKey("OrderId");
```

## Attribute names

By default, a property is stored in DynamoDB under its CLR property name. Use `HasAttributeName`
to override the store-level DynamoDB attribute name for a scalar property.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.Property(x => x.CustomerId).HasAttributeName("PK");
    b.Property(x => x.OrderId).HasAttributeName("SK");
});
```

The DynamoDB partition key attribute name is derived from `GetAttributeName()` on the partition
key property (falling back to the CLR property name). The same applies to the sort key.

For owned navigation attribute names (the containing map key in DynamoDB), see
[Owned Types](owned-types.md).

## Model validation

The provider validates the key configuration during model finalization and raises
`InvalidOperationException` for:

- A partition or sort key property that does not exist on the entity type.
- A partition or sort key property that is not a member of the EF primary key.
- Entity types sharing a DynamoDB table that disagree on the partition key attribute name or
  sort key attribute name.
- A sort key configured with no resolvable partition key.

## Owned and embedded types
- Complex navigation types are discovered as owned by convention.
- Primitive properties and supported primitive collection shapes remain scalar properties.
- Supported primitive collection shapes are:
  - lists: `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`
  - sets: `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`
  - dictionaries with string keys: `Dictionary<string,TValue>`, `IDictionary<string,TValue>`,
    `IReadOnlyDictionary<string,TValue>`, `ReadOnlyDictionary<string,TValue>`
- You can still configure ownership explicitly with `OwnsOne`/`OwnsMany` when needed.

## What works today
- `UseDynamo` registers provider services and query pipeline components.
- Table mapping uses a Dynamo-specific annotation (`Dynamo:TableName`).
- Composite keys are supported at the model level (for example `{ Pk, Sk }`).
- Convention-based partition/sort key discovery from property names (`PK`, `PartitionKey`, `SK`, `SortKey`).
- Explicit `HasPartitionKey`/`HasSortKey` fluent API with automatic EF primary key alignment.
- `HasAttributeName` on scalar properties to control the DynamoDB store attribute name.

## Access patterns and scans
- DynamoDB PartiQL `SELECT` can trigger a full table scan unless predicates include partition-key
  equality or partition-key `IN` conditions.
- Key-based predicates are important for predictable latency and cost.
- The provider currently does not add scan-denial guards; query shape is controlled by your LINQ.

## Not configurable yet
- `ConsistentRead` is not currently exposed as a provider option.

## External references
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
