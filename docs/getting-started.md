---
title: Getting Started
description: Install and configure the DynamoDB EF Core provider.
icon: lucide/rocket
---

# Getting Started

_This page walks through installing the provider, configuring a `DbContext`, mapping an entity to a
DynamoDB table, and running your first query and write._

## Prerequisites

- **.NET 10** and **EF Core 10**
- An **AWS account** with a DynamoDB table already created, or
    [DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
    running locally for development
- **AWS credentials** resolvable by the standard credential chain — environment variables,
    `~/.aws/credentials`, an IAM instance profile, etc.

The DynamoDB table must exist before the provider can query it. The provider does not create or
migrate tables.

## Installation

```bash
dotnet add package EntityFrameworkCore.DynamoDb
```

## Define an Entity

Each C# class maps to items in a DynamoDB table. Properties map to DynamoDB attributes.

```csharp
public class Product
{
    public required string Id { get; set; }
    public required string Category { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
```

Property names map to attribute names as-is by default. See
[Attribute Naming](configuration/attribute-naming.md) for conventions and overrides.

## Configure DbContext

### Direct configuration

Override `OnConfiguring` to set up the provider inline. This is the quickest path and works well for
console apps or tests pointing at DynamoDB Local:

```csharp
public class ShopContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(options =>
            options.ConfigureDynamoDbClientConfig(config =>
            {
                config.ServiceURL = "http://localhost:8000"; // DynamoDB Local
                config.AuthenticationRegion = "us-east-1";
                config.UseHttp = true;
            }));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasPartitionKey(p => p.Id);
        });
    }
}
```

!!! tip "Using dependency injection"

    `OnConfiguring` and DI are not mutually exclusive. A context with `OnConfiguring` can be
    registered as-is — the container handles lifetime, and the inline configuration handles provider
    options:

    ```csharp
    services.AddDbContext<ShopContext>();
    ```

    You can also pass the provider options through `AddDbContext` directly, which is useful when you
    want to supply a pre-configured `IAmazonDynamoDB` instance (for custom retry policies, endpoints,
    or credentials):

    ```csharp
    services.AddDbContext<ShopContext>(options =>
        options.UseDynamo(o => o.DynamoDbClient(dynamoClient)));
    ```

    See [Configuration](configuration/index.md) for all available options.

## Map the Entity

Inside `OnModelCreating`, every entity needs at minimum a table name and a partition key:

```csharp
modelBuilder.Entity<Product>(b =>
{
    b.ToTable("Products");          // DynamoDB table name — must already exist
    b.HasPartitionKey(p => p.Id);   // partition key attribute
});
```

If your table has a sort key, declare it too:

```csharp
b.HasSortKey(p => p.Category);
```

See [Entities and Keys](modeling/entities-keys.md) for composite keys, secondary indexes, and owned
types.

## Run a Query

```csharp
await using var context = new ShopContext();

// Retrieve all products (full scan — avoid on large tables)
var all = await context.Products.ToListAsync();

// Filter by partition key — efficient; hits a single partition
var product = await context.Products.FirstOrDefaultAsync(p => p.Id == "prod-42");
```

Always filter on the partition key when possible. Queries without a partition key condition perform
a full table scan, which is slow and expensive on large tables.

## Save Data

**Add**

```csharp
var product = new Product { Id = "prod-99", Category = "hardware", Name = "Widget", Price = 9.99m };
context.Products.Add(product);
await context.SaveChangesAsync();
```

**Update**

Mutate a tracked entity — one returned from a query on the same context — and call
`SaveChangesAsync`. EF Core's change tracker detects the modified properties and issues an UPDATE.

```csharp
var product = await context.Products.FirstOrDefaultAsync(p => p.Id == "prod-99");

product!.Price = 12.99m;
await context.SaveChangesAsync();
```

**Delete**

```csharp
var product = await context.Products.FirstOrDefaultAsync(p => p.Id == "prod-99");

context.Products.Remove(product!);
await context.SaveChangesAsync();
```

!!! warning "Everything is async"

    The DynamoDB SDK has no synchronous I/O. All operations — queries and writes alike — must use
    their async counterparts: `ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`,
    `SaveChangesAsync`, and so on. `ToList()`, `SaveChanges()`, and other synchronous methods are
    not supported and will throw.

## See Also

- [DynamoDB Concepts for EF Developers](dynamodb-concepts.md) — read this if DynamoDB is new to you
- [Configuration](configuration/index.md) — client setup, options, and `DbContext` registration
- [Data Modeling](modeling/index.md) — keys, complex properties, secondary indexes, and inheritance
- [Querying](querying/index.md) — supported LINQ operators, filtering, projection, and pagination
- [Limitations](limitations.md) — what is not supported and why
