---
icon: lucide/house
---

# EntityFrameworkCore.DynamoDb

Entity Framework Core provider for AWS DynamoDB.

This provider translates LINQ queries to PartiQL and executes them with the AWS SDK.

!!! warning

    This project is still under active development and is not production-ready yet.

## Install

```bash
dotnet add package --prerelease EntityFrameworkCore.DynamoDb
```

## Start here

1. Follow [Getting Started](getting-started.md) for package install, DynamoDB configuration,
    `DbContext` setup, entity/table mapping, and first query execution.
1. Review [Configuration](configuration.md) for client setup and transaction behavior.
1. Use [Operators](operators.md) to confirm which LINQ shapes translate today.
1. Read [Limitations](limitations.md) before adopting production query patterns.

## Hello world

Single-file minimal example from model + context to first query:

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class User
{
    public required string PK { get; set; }
    public required string SK { get; set; }
    public required string Email { get; set; }
}

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();

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
    }
}

await using var context = new AppDbContext();

var profile = await context.Users
    .Where(x => x.PK == "USER#42" && x.SK == "PROFILE")
    .FirstOrDefaultAsync();
```

For the full setup path, use [Getting Started](getting-started.md).

## Current Scope

- Async query execution is supported.
- `SaveChangesAsync` is implemented for Added/Modified/Deleted root entities.
- Synchronous `SaveChanges` is not supported (DynamoDB API is async-only).
- LINQ translation support is partial; [Operators](operators.md) is the source of truth.
- Includes support for table mapping, key mapping, owned types, and secondary-index metadata.

## Compatibility

- .NET target framework: `net10.0`
- EF Core version: `10.0.x`
- AWS SDK dependency: `AWSSDK.DynamoDBv2` `4.x`
- Works with Amazon DynamoDB and DynamoDB Local.

## Issues and Help

- Report bugs and request features on [GitHub Issues](https://github.com/LayeredCraft/dynamodb-efcore-provider/issues).
- For local debugging guidance, use [Diagnostics](diagnostics.md).
