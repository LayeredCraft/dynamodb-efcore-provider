using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.AotTests;

/// <summary>
///     Table lifecycle and seeding with a compiled model plus runtime resource names. Seed data comes
///     from the design-time model, which is always built at runtime, so this is exercised with a
///     compiled model in a JIT process; under Native AOT the design-time model is unavailable and the
///     lifecycle APIs are not part of that scenario.
/// </summary>
public partial class CompiledModelExecutionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Compiled_model_with_runtime_names_creates_seeds_and_deletes_the_runtime_resources()
    {
        using var compiled = CompileModel<LifecycleContext>("CompiledLifecycle");
        var tables = new FakeTables();
        var statements = new List<string>();
        CurrentTables.Value = tables;
        CurrentStatements.Value = statements;

        await using var context = new LifecycleContext(
            new DbContextOptionsBuilder<LifecycleContext>()
                .UseDynamo(configure => configure
                    .DynamoDbClient(SharedRecordingClient.Value)
                    .RuntimeResourceNames(names => names
                        .Table("Life", "life-real-table")
                        .SecondaryIndex("Life", "ByOwner", "life-real-gsi")))
                .UseModel(compiled.Model)
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

        // The context really runs on the compiled model.
        context.Model.GetType().FullName.Should().Be("CompiledLifecycle.LifecycleContextModel");

        (await context.Database.EnsureCreatedAsync()).Should().BeTrue();

        // The table and its index are created under the runtime names only.
        var created = tables.Created.Should().ContainSingle().Subject;
        created.TableName.Should().Be("life-real-table");
        created.GlobalSecondaryIndexes.Should().ContainSingle().Which.IndexName.Should().Be("life-real-gsi");

        // Seed data for both the base type and the derived type lands in that table.
        var inserts = statements.Where(static s => s.StartsWith("INSERT", StringComparison.Ordinal)).ToArray();
        inserts.Should().HaveCount(2);
        inserts.Should().OnlyContain(s => s.Contains("INSERT INTO \"life-real-table\""));
        statements.Should().NotContain(s => s.Contains("life-design-table"));

        await context.Database.EnsureDeletedAsync();
        tables.Deleted.Should().Equal("life-real-table");
    }
}

public sealed class LifecycleContext : DbContext
{
    public LifecycleContext()
        : base(new DbContextOptionsBuilder<LifecycleContext>().UseDynamo().Options)
    {
    }

    public LifecycleContext(DbContextOptions<LifecycleContext> options)
        : base(options)
    {
    }

    public DbSet<LifeAnimal> Animals => Set<LifeAnimal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LifeAnimal>(entity =>
        {
            DynamoEntityTypeBuilderExtensions
                .ToTable(entity, "life-design-table")
                .HasLogicalTableName("Life");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .HasGlobalSecondaryIndex("ByOwner", nameof(LifeAnimal.Owner))
                .HasSecondaryIndexName("life-design-gsi");
            entity.HasData(new LifeAnimal { Pk = "animal", Owner = "owner" });
        });
        modelBuilder.Entity<LifeDog>(entity
            => entity.HasData(new LifeDog { Pk = "dog", Owner = "owner" }));
    }
}

public class LifeAnimal
{
    public string Pk { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;
}

public sealed class LifeDog : LifeAnimal;
