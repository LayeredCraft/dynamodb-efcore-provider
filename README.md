# EntityFrameworkCore.DynamoDb

EF Core Provider for AWS DynamoDB

Documentation: <https://dynamodb-ef-core.layeredcraft.dev/>

Contributing: [CONTRIBUTING.md](CONTRIBUTING.md)

Write EF Core models against DynamoDB tables, compose queries with LINQ, and let the provider
translate supported query shapes into PartiQL executed through the AWS SDK.

## Installation

| Provider package                    | Target framework | EF Core | .NET    |
| ----------------------------------- | ---------------- | ------- | ------- |
| `EntityFrameworkCore.DynamoDb` 10.x | `net10.0`        | 10.0.x  | .NET 10 |
| `EntityFrameworkCore.DynamoDb` 11.x | `net11.0`        | 11.0.x  | .NET 11 |

Each package targets one framework and one EF Core major version. Match the provider's major
version to your EF Core major version: 11.x packages cannot be installed in a `net10.0` project,
and 10.x packages do not support EF Core 11. The exact EF Core range is listed on each package's
NuGet page — for example, 10.2.0 requires EF Core 10.0.14 or later 10.0.x, and 11.0.0 requires
EF Core 11.0.0 or later 11.0.x.

```bash
# .NET 10 / EF Core 10
dotnet add package EntityFrameworkCore.DynamoDb --version 10.2.0

# .NET 11 / EF Core 11
dotnet add package EntityFrameworkCore.DynamoDb --version 11.0.0
```

Use the latest release of the line that matches your EF Core version. Always pass `--version` on
.NET 10: without it, NuGet selects the newest stable package, which is an 11.x package that cannot
be installed in a `net10.0` project. Packages with a `-preview` suffix are CI builds from `main`;
do not use them in production.

See [Choose a package version](https://dynamodb-ef-core.layeredcraft.dev/getting-started/#choose-a-package-version)
for central package management and multi-targeted projects.

Review the [limitations](https://dynamodb-ef-core.layeredcraft.dev/limitations/) before adopting
the provider; DynamoDB's access-pattern model means some EF Core features and LINQ shapes are not
supported.

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
            b.ToDynamoTable("App");
            b.HasPartitionKey(x => x.PK);
            b.HasSortKey(x => x.SK);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToDynamoTable("App");
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

- Query execution is asynchronous only, including generated precompiled queries.
- Precompiled-query and NativeAOT support are experimental. See
  [the documented restrictions](docs/querying/precompiled-queries.md) before using them in production.
- `SaveChangesAsync` is implemented for Added/Modified/Deleted root entities.
- Synchronous `SaveChanges` is not supported (DynamoDB API is async-only).
- LINQ support is partial; use `docs/querying/operators.md` as the source of truth for supported
  query shapes.
- Table mapping, key mapping, complex properties and complex collections, and secondary-index
  metadata are supported. Owned entity types are not supported; use complex types instead.
- Optimistic concurrency is opt-in via `.IsConcurrencyToken()` / `[ConcurrencyCheck]`; token
  values are application-managed.
