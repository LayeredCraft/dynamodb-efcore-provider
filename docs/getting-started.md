---
icon: lucide/rocket
---

# Getting Started

This guide walks through the first end-to-end setup for the provider: install the package, configure DynamoDB, define your context and entity mapping, then execute your first query.

## Add package

```bash
dotnet add package --prerelease EntityFrameworkCore.DynamoDb
```

## Configure DynamoDB

Configure the provider once in your application startup (for example `Program.cs`) using `UseDynamo(...)`.

For local development (for example DynamoDB Local), configure the AWS SDK client endpoint in `AddDbContext`:

```csharp
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<AppDbContext>(optionsBuilder =>
    optionsBuilder.UseDynamo(options =>
    {
        options.ConfigureDynamoDbClientConfig(config =>
        {
            config.ServiceURL = "http://localhost:8000";
            config.AuthenticationRegion = "us-east-1";
            config.UseHttp = true;
        });
    }));
```

!!! note

    Use one configuration location for your app. Prefer startup/DI (`AddDbContext`) and keep
    `DbContext` focused on model mapping. Use `OnConfiguring` only for special cases such as
    small samples or tests where DI is not used.

See [Configuration](configuration.md) for client precedence, advanced options, and transaction settings.

## Create context

Define a `DbContext` and register your `DbSet`.

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("App");
            b.HasPartitionKey(x => x.PK);
            b.HasSortKey(x => x.SK);
        });
    }
}
```

## Define table/entity

The provider maps to an existing DynamoDB table. Create the table in AWS (or DynamoDB Local) and map the entity to that table name and key schema.

```csharp
public sealed class User
{
    public required string PK { get; set; }
    public required string SK { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
}
```

```csharp
modelBuilder.Entity<User>(b =>
{
    b.ToTable("App");
    b.HasPartitionKey(x => x.PK);
    b.HasSortKey(x => x.SK);
});
```

!!! note

    Root DynamoDB entities should use `HasPartitionKey(...)` / `HasSortKey(...)`.
    Do not configure `HasKey(...)` directly on root entities.

See [Indexes](indexes.md) for key conventions and secondary index configuration.

## Execute query

Start with key-based query shapes for predictable behavior.
Assuming `context` is resolved from DI:

```csharp
var profile = await context.Users
    .Where(x => x.PK == "USER#42" && x.SK == "PROFILE")
    .FirstOrDefaultAsync();
```

```csharp
var users = await context.Users
    .Where(x => x.PK == "ORG#7" && x.SK.StartsWith("USER#"))
    .OrderBy(x => x.SK)
    .ToListAsync();
```

!!! note

    Operator support is intentionally strict. If a LINQ shape is unsupported, translation fails rather than silently switching to client evaluation.
    Use [Operators](operators.md) as the source of truth.

## Next steps

- [Configuration](configuration.md)
- [Indexes](indexes.md)
- [Operators](operators.md)
- [Limitations](limitations.md)
- [Diagnostics](diagnostics.md)
