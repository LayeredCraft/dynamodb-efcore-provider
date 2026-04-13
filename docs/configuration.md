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
});
```

## Options

- `DynamoDbClient`: use a preconfigured `IAmazonDynamoDB` instance.
- `DynamoDbClientConfig`: use a preconfigured `AmazonDynamoDBConfig` when creating the SDK client.
- `ConfigureDynamoDbClientConfig`: apply a callback to configure `AmazonDynamoDBConfig` before client creation.

Per-query evaluation budget is controlled by `.Limit(n)` on the query rather than a global default.
See [Pagination](pagination.md) for the evaluation budget model.

## SaveChanges transaction behavior

The provider follows EF Core `Database.AutoTransactionBehavior` for implicit transaction policy:

- `WhenNeeded` (default): one root write executes directly; multi-root saves execute atomically via DynamoDB `ExecuteTransaction`.
- `Always`: requires transactional execution for multi-root saves.
- `Never`: disables implicit transactions and executes root writes independently.

```csharp
context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
```

For transactional overflow (when one transaction cannot hold the whole write unit), configure:

- `TransactionOverflowBehavior`:
    - `Throw` (default)
    - `UseChunking` (splits into multiple `ExecuteTransaction` calls)
- `MaxTransactionSize` (default `100`, valid range `1..100`)

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
    options.MaxTransactionSize(50);
});
```

Per-context overrides are available on `DatabaseFacade`:

```csharp
context.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
context.Database.SetMaxTransactionSize(25);
```

Configuration precedence:

1. Per-context override (`context.Database.Set...`)
1. Startup/provider option (`UseDynamo(...)`)
1. Provider defaults (`Throw`, `100`)

`UseChunking` keeps each chunk atomic, but the overall `SaveChanges` call is no longer globally
atomic across all root writes.

## Client configuration precedence

- The provider resolves client settings in this order:
    1. `DynamoDbClient(...)` (explicit client instance)
    1. `DynamoDbClientConfig(...)` (base SDK config)
    1. `ConfigureDynamoDbClientConfig(...)` (callback adjustments)

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
        .HasPartitionKey(x => x.Pk);
}
```

## Key configuration

DynamoDB tables have a partition key and an optional sort key. The provider needs to know which
EF properties map to those keys so it can build correct key expressions.

For root DynamoDB entities, `HasKey(...)` is not supported. Configure `HasPartitionKey(...)` and
optional `HasSortKey(...)` instead; the provider derives the EF primary key automatically from that
DynamoDB key mapping.

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
    // EF primary key is automatically aligned to [CustomerId, OrderId]
});
```

When `HasPartitionKey` and/or `HasSortKey` are set, the provider automatically configures the EF
primary key to match. Root entities should not configure `HasKey(...)` themselves.

### `HasKey(...)` is rejected for root entities

This model is **not** enough:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasKey(x => new { x.CustomerId, x.OrderId });
});
```

The provider rejects that model for root entities. Use Dynamo-specific key mapping instead:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);
});
```

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

## Shared-table discriminator (`$type`)

When multiple instantiable concrete entity types map to the same DynamoDB table, the provider
configures and validates a discriminator automatically.

- Default discriminator attribute name: `$type`.
- Default discriminator value: EF Core type short name (for example `UserEntity`).
- Discriminator values must be unique within a shared table group.
- Discriminator attribute names must be consistent within a shared table group.
- Discriminator attribute name must not collide with resolved PK/SK attribute names.

Use EF Core to override the discriminator attribute name:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasEmbeddedDiscriminatorName("$kind");

    modelBuilder.Entity<UserEntity>().ToTable("App");
    modelBuilder.Entity<OrderEntity>().ToTable("App");
}
```

Discriminator filtering is applied to shared-table root queries on supported operator shapes. In
DynamoDB this is a non-key predicate, so it can reduce returned items without necessarily reducing
items read/evaluated.

### Inheritance behavior

When a mapped hierarchy uses a shared table, discriminator filtering follows EF Core inheritance
semantics:

- Querying `DbSet<BaseType>` includes all concrete discriminator values in that hierarchy.
- Querying `DbSet<DerivedType>` includes concrete values in that derived subtree.
- Abstract entity types are not materialized.

To support correct derived-type materialization for base queries, the provider projects hierarchy
attributes needed by concrete derived types.

## Model validation

The provider validates the key configuration during model finalization and raises
`InvalidOperationException` for:

- A partition or sort key property that does not exist on the entity type.
- An explicit root EF primary key configured with `HasKey(...)` or `[Key]`.
- An internally derived EF primary key that does not match the configured DynamoDB key shape.
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
- Secondary-index metadata APIs:
    - `HasGlobalSecondaryIndex(...)`
    - `HasLocalSecondaryIndex(...)`
    - `WithIndex(...)`
- `HasAttributeName` on scalar properties to control the DynamoDB store attribute name.

## Access patterns and scans

- DynamoDB PartiQL `SELECT` can trigger a full table scan unless predicates include partition-key
    equality or partition-key `IN` conditions.
- Key-based predicates are important for predictable latency and cost.
- The provider currently does not add scan-denial guards; query shape is controlled by your LINQ.

## Not configurable yet

- `ConsistentRead` is not currently exposed as a provider option.
- Provider-side key encoding helpers.

## Secondary indexes

The provider now exposes model-configuration APIs for GSIs and LSIs:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);

    b.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
    b.HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAt);
    b.HasLocalSecondaryIndex("ByStatusDate", x => x.CreatedAt);
});
```

Current scope for these APIs:

- GSI key schema is always explicit.
- LSI requires a resolved table partition key and sort key from `HasPartitionKey(...)` /
    `HasSortKey(...)` or Dynamo naming conventions.
- `WithIndex(...)` emits `FROM "Table"."Index"` for explicit query-time targeting.
- Automatic index selection can route compatible queries to GSIs/LSIs when enabled.
- Automatic selection only considers indexes visible to the queried entity type and avoids
    subtype-only sparse indexes for base-type queries.

See [Indexes](indexes.md) for the current index configuration and support model.

## External references

- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
