---
icon: lucide/house
title: EntityFrameworkCore.DynamoDb
description: Entity Framework Core provider for Amazon DynamoDB — write LINQ queries, get PartiQL.
---

# EntityFrameworkCore.DynamoDb

_Use EF Core's familiar LINQ API against Amazon DynamoDB. Queries are translated to PartiQL and executed via the AWS SDK — no manual AttributeValue wrangling required._

[![NuGet](https://img.shields.io/nuget/v/EntityFrameworkCore.DynamoDb.svg)](https://www.nuget.org/packages/EntityFrameworkCore.DynamoDb)

!!! note "Community project"

    This is an independent, community-maintained library. It is not affiliated with, endorsed by, or supported by Amazon Web Services or Microsoft.

## Supported versions

| Provider package                    | Target framework | EF Core | .NET    |
| ----------------------------------- | ---------------- | ------- | ------- |
| `EntityFrameworkCore.DynamoDb` 10.x | `net10.0`        | 10.0.x  | .NET 10 |
| `EntityFrameworkCore.DynamoDb` 11.x | `net11.0`        | 11.0.x  | .NET 11 |

Each package targets one framework and one EF Core major version. Match the provider's major
version to your EF Core major version: 11.x packages cannot be installed in a `net10.0` project,
and 10.x packages do not support EF Core 11. The exact EF Core range is listed on each package's
NuGet page — for example, 10.2.0 requires EF Core 10.0.14 or later 10.0.x, and 11.0.0 requires
EF Core 11.0.0 or later 11.0.x.

## Install

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

See [Choose a package version](getting-started.md#choose-a-package-version) for central package
management and multi-targeted projects.

## Quick Example

```csharp
// Define your entity
public class Order
{
    public string Pk { get; set; } = null!;   // e.g. "CUSTOMER#cust-42"
    public string Sk { get; set; } = null!;   // e.g. "ORDER#ord-99"
    public decimal Total { get; set; }
}

// Configure your DbContext
public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasPartitionKey(o => o.Pk);
            b.HasSortKey(o => o.Sk);
        });
    }
}

// Create a DbContext
var options = new DbContextOptionsBuilder<ShopContext>()
    .UseDynamo()
    .Options;

await using var context = new ShopContext(options);

// Query
var orders = await context.Orders
    .Where(o => o.Pk == "CUSTOMER#cust-42" && o.Sk.StartsWith("ORDER#") && o.Total > 100m)
    .OrderBy(o => o.Sk)
    .ToListAsync();
```

## Key Features

- **LINQ to PartiQL** — `Where`, `Select`, `OrderBy`, `Limit(n)`, and more translated server-side
- **Complex properties and collections** — map nested documents as complex types and complex collections
- **Secondary indexes** — query GSIs and LSIs with index hints
- **Optimistic concurrency** — version tokens via EF Core's `IsConcurrencyToken`
- **Pagination** — cursor-based pagination using DynamoDB's `NextToken`
- **Type mappings** — `Guid`, `DateTime`, `DateOnly`, `TimeOnly`, `enum`, `byte[]`, and more

## Explore the Docs

<div class="grid cards" markdown>

- :lucide-rocket: **[Getting Started](getting-started.md)**

    Install the package, configure your `DbContext`, and run your first query.

- :lucide-book-open: **[DynamoDB Concepts for EF Developers](dynamodb-concepts.md)**

    Understand how DynamoDB's partition model maps to EF Core concepts.

- :lucide-settings: **[Configuration](configuration/index.md)**

    Client setup, table and key mapping, attribute naming, and `DbContext` options.

- :lucide-database: **[Data Modeling](modeling/index.md)**

    Entities, keys, complex properties, secondary indexes, and inheritance.

- :lucide-search: **[Querying](querying/index.md)**

    Supported LINQ operators, filtering, projection, ordering, and pagination.

- :lucide-save: **[Saving Data](saving/index.md)**

    Add, update, delete, transactions, and optimistic concurrency.

</div>

## Limitations

This provider does not support all EF Core features. DynamoDB's access-pattern model means some LINQ shapes cannot be translated. See [Limitations](limitations.md) for the authoritative list of what is not supported and why.

## License

MIT. See [LICENSE](https://github.com/LayeredCraft/dynamodb-efcore-provider/blob/main/LICENSE) on GitHub.
