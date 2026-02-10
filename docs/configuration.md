---
icon: lucide/settings
---

# Configuration

## DbContext setup
- Configure the provider with `UseDynamo()` or `UseDynamo(options => ...)`.
- For local development and integration tests, set `ServiceUrl` to DynamoDB Local.

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.ServiceUrl("http://localhost:8000");
    options.AuthenticationRegion("us-east-1");
    options.DefaultPageSize(100);
});
```

## Options
- `ServiceUrl`: target DynamoDB Local or a custom endpoint.
- `AuthenticationRegion`: AWS region used by the SDK client.
- `DefaultPageSize`: default request page size (`ExecuteStatementRequest.Limit`) for queries when no per-query override is present.
- `DefaultPageSize` must be greater than zero.

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

## What works today
- `UseDynamo` registers provider services and query pipeline components.
- Table mapping uses a Dynamo-specific annotation (`Dynamo:TableName`).
- Composite keys are supported at the model level (for example `{ Pk, Sk }`).

## Access patterns and scans
- DynamoDB PartiQL `SELECT` can trigger a full table scan unless predicates include partition-key
  equality or partition-key `IN` conditions.
- Key-based predicates are important for predictable latency and cost.
- The provider currently does not add scan-denial guards; query shape is controlled by your LINQ.

## Not configurable yet
- `ConsistentRead` is not currently exposed as a provider option.

## External references
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
