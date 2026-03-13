# LayeredCraft.EntityFrameworkCore.DynamoDb

EF Core Provider for Amazon DynamoDB.

> [!WARNING]
> This provider is not production ready yet. Validate behavior carefully before using it in
> important workloads, and review the current limitations before adopting it.

Write EF Core models against DynamoDB tables, compose queries with LINQ, and let the provider
translate supported query shapes into PartiQL executed through the AWS SDK.

## Why It Exists

- Brings an EF Core-style modeling and querying experience to DynamoDB.
- Translates supported LINQ queries into DynamoDB PartiQL.
- Supports DynamoDB-focused mapping concepts such as partition keys, sort keys, and secondary
  indexes.

## Quick Look

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class AppContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(options =>
        {
            options.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL = "http://localhost:8000";
                config.AuthenticationRegion = "us-east-1";
                config.UseHttp = true;
            });
        });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("App");
            b.HasPartitionKey(x => x.PK);
            b.HasSortKey(x => x.SK);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("App");
            b.HasPartitionKey(x => x.PK);
            b.HasSortKey(x => x.SK);
        });
    }
}

public sealed class User
{
    public required string PK { get; set; }
    public required string SK { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
}

public sealed class Order
{
    public required string PK { get; set; }
    public required string SK { get; set; }
    public required string Status { get; set; }
    public decimal Total { get; set; }
}

var user = await context.Users
    .Where(x => x.PK == "USER#42" && x.SK == "PROFILE")
    .SingleAsync();

var openOrders = await context.Orders
    .Where(x => x.PK == "USER#42" && x.SK.StartsWith("ORDER#") && x.Status == "Pending")
    .OrderBy(x => x.SK)
    .ToListAsync();
```

## Current Scope

- Async query execution is supported.
- `SaveChanges` and `SaveChangesAsync` are not implemented yet.
- LINQ support is partial; use `docs/operators.md` as the source of truth for supported query
  shapes.
- Table mapping, key mapping, owned types, and secondary-index metadata are supported.
