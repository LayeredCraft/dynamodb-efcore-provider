---
title: Getting Started
description: Install and configure the DynamoDB EF Core provider.
icon: lucide/rocket
---

# Getting Started

_This page walks through installing the provider, configuring a `DbContext`, mapping an entity to a
DynamoDB table, and running your first query and write._

## Prerequisites

- **.NET 10 with EF Core 10**, or **.NET 11 with EF Core 11** — see
    [Choose a package version](#choose-a-package-version)
- An **AWS account** with a DynamoDB table already created, or
    [DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html)
    running locally for development
- **AWS credentials** resolvable by the standard credential chain — environment variables,
    `~/.aws/credentials`, an IAM instance profile, etc.

The DynamoDB table must exist before the provider can query it. The provider does not create or
migrate tables.

## Installation

### Choose a package version

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

### Central package management

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="EntityFrameworkCore.DynamoDb" Version="10.2.0" />
```

Use `11.0.0` for .NET 11 projects.

### Multi-targeted applications and upgrading

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="EntityFrameworkCore.DynamoDb" Version="10.2.0" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
  <PackageReference Include="EntityFrameworkCore.DynamoDb" Version="11.0.0" />
</ItemGroup>
```

To move from 10.x to 11.x, retarget the project to `net11.0`, update every
`Microsoft.EntityFrameworkCore.*` package (including `Tasks` and `Design` if you use precompiled
queries) to 11.0.x, and then update the provider to 11.x.

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
            b.ToDynamoTable("Products");
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
    b.ToDynamoTable("Products");    // DynamoDB table name — must already exist
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

!!! note "Query execution is async-only"

    Normal and generated precompiled queries are asynchronous only. Use methods such as
    `ToListAsync()` and `FirstOrDefaultAsync()` for queries and `SaveChangesAsync()` for writes.

## See Also

- [DynamoDB Concepts for EF Developers](dynamodb-concepts.md) — read this if DynamoDB is new to you
- [Configuration](configuration/index.md) — client setup, options, and `DbContext` registration
- [Data Modeling](modeling/index.md) — keys, complex properties, secondary indexes, and inheritance
- [Querying](querying/index.md) — supported LINQ operators, filtering, projection, and pagination
- [Limitations](limitations.md) — what is not supported and why
