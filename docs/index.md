---
icon: lucide/house
title: EntityFrameworkCore.DynamoDb
description: Entity Framework Core provider for Amazon DynamoDB — write LINQ queries, get PartiQL.
---

# EntityFrameworkCore.DynamoDb

_Use EF Core's familiar LINQ API against Amazon DynamoDB. Queries are translated to PartiQL and executed via the AWS SDK — no manual AttributeValue wrangling required._

## Install

```bash
dotnet add package EntityFrameworkCore.DynamoDb
```

## Quick Example

```csharp
// Define your model
public class Order
{
    public string CustomerId { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public decimal Total { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

// Configure your DbContext
public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasPartitionKey(o => o.CustomerId);
            b.HasSortKey(o => o.OrderId);
            b.OwnsMany(o => o.Lines);
        });
    }
}

// Register and query
services.AddDbContext<ShopContext>(options =>
    options.UseDynamoDb(dynamo => dynamo.UseAmazonDynamoDB()));

var orders = await context.Orders
    .Where(o => o.CustomerId == "cust-42" && o.Total > 100m)
    .OrderBy(o => o.OrderId)
    .ToListAsync();
```

## Key Features

- **LINQ to PartiQL** — `Where`, `Select`, `OrderBy`, `Take`, and more translated server-side
- **Owned types and collections** — map nested documents as owned entities or owned collections
- **Secondary indexes** — query GSIs and LSIs with index hints
- **Optimistic concurrency** — version tokens via EF Core's `IsConcurrencyToken`
- **Pagination** — cursor-based pagination using DynamoDB's `LastEvaluatedKey`
- **Type mappings** — `Guid`, `DateTime`, `DateOnly`, `TimeOnly`, `enum`, `byte[]`, and more

## Explore the Docs

<div class="grid cards" markdown>

- **[Getting Started](getting-started.md)**

    Install the package, configure your `DbContext`, and run your first query.

- **[DynamoDB Concepts for EF Developers](dynamodb-concepts.md)**

    Understand how DynamoDB's partition model maps to EF Core concepts.

- **[Configuration](configuration/index.md)**

    Client setup, table and key mapping, attribute naming, and `DbContext` options.

- **[Data Modeling](modeling/index.md)**

    Entities, keys, owned types, secondary indexes, and inheritance.

- **[Querying](querying/index.md)**

    Supported LINQ operators, filtering, projection, ordering, and pagination.

- **[Saving Data](saving/index.md)**

    Add, update, delete, transactions, and optimistic concurrency.

</div>

## Limitations

This provider does not support all EF Core features. DynamoDB's access-pattern model means some LINQ shapes cannot be translated. See [Limitations](limitations.md) for the authoritative list of what is not supported and why.

## License

MIT. See [LICENSE](https://github.com/LayeredCraft/dynamodb-efcore-provider/blob/main/LICENSE) on GitHub.
