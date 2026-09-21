using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>
///     Runtime physical resource names: a logical table identity declared in the model plus the EF
///     index name as the logical index identity, mapped to environment-specific physical names through
///     <c>UseDynamo(o =&gt; o.RuntimeResourceNames(...))</c>. These cases use a JIT-built model; the
///     compiled-model and Native AOT cases live in the AOT test projects.
/// </summary>
public class RuntimeResourceNamesTests
{
    private const string PlaceholderTable = "placeholder-table";
    private const string PlaceholderIndex = "placeholder-index";
    private const string RealTable = "real-table";
    private const string RealIndex = "real-index";

    private static void MapItems(DynamoRuntimeResourceNamesBuilder names)
        => names.Table("Items", RealTable).SecondaryIndex("Items", "ByCategory", RealIndex);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Jit_model_without_runtime_mapping_uses_model_resource_names()
    {
        var statements = new List<string>();

        await RunAsync(statements, map: null);

        statements.Should().Contain(s => s.Contains($"FROM \"{PlaceholderTable}\" WHERE"));
        statements.Should().Contain(
            s => s.Contains($"FROM \"{PlaceholderTable}\".\"{PlaceholderIndex}\""));
        statements.Should().NotContain(s => s.Contains(RealTable) || s.Contains(RealIndex));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Jit_model_runtime_mapping_takes_precedence_over_model_configuration()
    {
        var statements = new List<string>();

        await RunAsync(statements, MapItems);

        statements.Should().Contain(s => s.Contains($"FROM \"{RealTable}\" WHERE"));
        statements.Should().Contain(s => s.Contains($"FROM \"{RealTable}\".\"{RealIndex}\""));
        statements.Should().Contain(s => s.Contains($"INSERT INTO \"{RealTable}\""));
        statements.Should().NotContain(s => s.Contains($"\"{PlaceholderTable}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Index_can_be_mapped_without_mapping_its_table()
    {
        var statements = new List<string>();

        await RunAsync(
            statements,
            names => names.SecondaryIndex("Items", "ByCategory", RealIndex));

        statements.Should().Contain(
            s => s.Contains($"FROM \"{PlaceholderTable}\".\"{RealIndex}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Logical_table_declared_on_one_entity_applies_to_the_whole_table()
    {
        var statements = new List<string>();
        CurrentStatements.Value = statements;

        await using var context = new SharedTableContext(
            new DbContextOptionsBuilder<SharedTableContext>()
                .UseDynamo(configure => configure
                    .DynamoDbClient(SharedRecordingClient.Value)
                    .RuntimeResourceNames(names => names.Table("Shared", "mapped-shared-table")))
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);
        _ = await context.First.Where(item => item.Pk == "1").ToListAsync();
        _ = await context.Second.Where(item => item.Pk == "1").ToListAsync();

        // Only one entity type declares the logical name; both belong to the same table.
        statements.Should().HaveCount(2);
        statements.Should().OnlyContain(s => s.Contains("FROM \"mapped-shared-table\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Unknown_logical_table_fails_fast_and_names_the_declared_tables()
    {
        var act = () => RunAsync(
            [],
            names => names.Table("Itms", RealTable));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*logical table 'Itms'*Declared logical tables: 'Items', 'Others'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Unknown_logical_index_fails_fast_and_names_the_declared_indexes()
    {
        var act = () => RunAsync(
            [],
            names => names.SecondaryIndex("Items", "ByCatgory", RealIndex));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*secondary index 'ByCatgory' of logical table 'Items'*Declared secondary indexes: 'ByCategory'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Index_mapping_for_an_unknown_logical_table_fails_fast()
    {
        var act = () => RunAsync(
            [],
            names => names.SecondaryIndex("Nope", "ByCategory", RealIndex));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*logical table 'Nope'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Duplicate_runtime_mappings_are_rejected_when_configured()
    {
        var table = () => new DynamoRuntimeResourceNamesBuilder()
            .Table("Items", RealTable)
            .Table("Items", "another");
        var index = () => new DynamoRuntimeResourceNamesBuilder()
            .SecondaryIndex("Items", "ByCategory", RealIndex)
            .SecondaryIndex("Items", "ByCategory", "another");

        table.Should().Throw<InvalidOperationException>().WithMessage("*'Items'*more than once*");
        index.Should().Throw<InvalidOperationException>().WithMessage("*'ByCategory'*more than once*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Runtime_mapping_names_must_not_be_empty()
    {
        var act = () => new DynamoRuntimeResourceNamesBuilder().Table("Items", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Equal_runtime_mappings_compare_equal_and_share_a_hash_code()
    {
        var first = new DynamoRuntimeResourceNamesBuilder();
        MapItems(first);
        var second = new DynamoRuntimeResourceNamesBuilder();
        second.SecondaryIndex("Items", "ByCategory", RealIndex).Table("Items", RealTable);
        var different = new DynamoRuntimeResourceNamesBuilder();
        different.Table("Items", "another");

        var a = first.Build();
        var b = second.Build();

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(different.Build());
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Conflicting_logical_table_names_within_a_table_fail_model_validation()
    {
        using var context = new ConflictingLogicalNamesContext(
            new DbContextOptionsBuilder<ConflictingLogicalNamesContext>()
                .UseDynamo()
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

        var act = () => _ = context.Model;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*conflicting logical table names*'Alpha'*'Beta'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void A_logical_table_name_declared_by_two_tables_fails_model_validation()
    {
        using var context = new DuplicateLogicalNameContext(
            new DbContextOptionsBuilder<DuplicateLogicalNameContext>()
                .UseDynamo()
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

        var act = () => _ = context.Model;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*logical table name 'Same'*more than one DynamoDB table*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task With_index_matches_the_effective_runtime_index_name_in_a_jit_model()
    {
        var statements = new List<string>();
        CurrentStatements.Value = statements;

        await using var context = new RuntimeNamesJitContext(
            new DbContextOptionsBuilder<RuntimeNamesJitContext>()
                .UseDynamo(configure => configure
                    .DynamoDbClient(SharedRecordingClient.Value)
                    .RuntimeResourceNames(MapItems))
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);
        _ = await context
            .Items
            .WithIndex(RealIndex)
            .Where(item => item.Category == "c")
            .ToListAsync();

        statements.Should().ContainSingle(s => s.Contains($"FROM \"{RealTable}\".\"{RealIndex}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Two_logical_tables_mapped_to_one_physical_table_fail_fast()
    {
        var act = () => RunAsync(
            [],
            names => names.Table("Items", "same-table").Table("Others", "same-table"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*more than one table to the physical table 'same-table'*'Items'*'Others'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task A_mapped_table_may_not_collide_with_an_unmapped_tables_model_name()
    {
        // 'Items' is remapped onto the physical name that 'Others' still uses.
        var act = () => RunAsync([], names => names.Table("Items", "other-placeholder-table"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*more than one table to the physical table 'other-placeholder-table'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Two_indexes_of_one_table_mapped_to_one_physical_index_fail_fast()
    {
        var statements = new List<string>();
        CurrentStatements.Value = statements;

        await using var context = new TwoIndexContext(
            new DbContextOptionsBuilder<TwoIndexContext>()
                .UseDynamo(configure => configure
                    .DynamoDbClient(SharedRecordingClient.Value)
                    .RuntimeResourceNames(names => names
                        .SecondaryIndex("Two", "ByA", "same-index")
                        .SecondaryIndex("Two", "ByB", "same-index")))
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

        var act = () => context.Rows.Where(row => row.Pk == "1").ToListAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*more than one secondary index of logical table 'Two' to the physical index 'same-index'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task With_index_argument_is_the_physical_index_name_and_not_the_logical_identity()
    {
        // No runtime mapping: the argument is the physical index name configured in the model. The EF
        // index name (the logical identity) is not accepted while it differs from the physical name.
        var physical = await TryWithIndexAsync(PlaceholderIndex, map: null);
        var logical = await TryWithIndexAsync("ByCategory", map: null);

        physical.Should().BeNull();
        logical.Should().NotBeNull().And.Contain("ByCategory");

        // With a runtime mapping in a JIT model: the argument is the runtime physical index name. The
        // logical identity and the design-time physical name are both rejected.
        (await TryWithIndexAsync(RealIndex, MapItems)).Should().BeNull();
        (await TryWithIndexAsync("ByCategory", MapItems)).Should().NotBeNull();
        (await TryWithIndexAsync(PlaceholderIndex, MapItems)).Should().NotBeNull();
    }

    private static async Task<string?> TryWithIndexAsync(
        string indexName,
        Action<DynamoRuntimeResourceNamesBuilder>? map)
    {
        CurrentStatements.Value = [];
        await using var context = new RuntimeNamesJitContext(
            new DbContextOptionsBuilder<RuntimeNamesJitContext>()
                .UseDynamo(configure =>
                {
                    configure.DynamoDbClient(SharedRecordingClient.Value);
                    if (map is not null)
                        configure.RuntimeResourceNames(map);
                })
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);
        try
        {
            _ = await context.Items.WithIndex(indexName).Where(item => item.Category == "c").ToListAsync();
            return null;
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    private static async Task RunAsync(
        List<string> statements,
        Action<DynamoRuntimeResourceNamesBuilder>? map)
    {
        CurrentStatements.Value = statements;
        await using var context = new RuntimeNamesJitContext(
            new DbContextOptionsBuilder<RuntimeNamesJitContext>()
                .UseDynamo(configure =>
                {
                    configure.DynamoDbClient(SharedRecordingClient.Value);
                    if (map is not null)
                        configure.RuntimeResourceNames(map);
                })
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

        _ = await context.Items.Where(item => item.Pk == "1").ToListAsync();
        _ = await context.Items.Where(item => item.Category == "c").ToListAsync();
        _ = await context.Others.Where(item => item.Pk == "1").ToListAsync();
        _ = await context.Others.Where(item => item.Kind == "k").ToListAsync();

        context.Items.Add(new RuntimeNamesJitItem { Pk = "2", Category = "c" });
        await context.SaveChangesAsync();
    }

    private static string Normalize(string statement)
        => Regex.Replace(statement, "\\s+", " ").Trim();

    private static readonly AsyncLocal<List<string>?> CurrentStatements = new();

    private static readonly Lazy<IAmazonDynamoDB> SharedRecordingClient =
        new(CreateRecordingClient);

    private static IAmazonDynamoDB CreateRecordingClient()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                CurrentStatements.Value?.Add(
                    Normalize(callInfo.Arg<ExecuteStatementRequest>()!.Statement));
                return Task.FromResult(new ExecuteStatementResponse { Items = [] });
            });
        client
            .ExecuteTransactionAsync(
                Arg.Any<ExecuteTransactionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                CurrentStatements.Value?.AddRange(
                    callInfo
                        .Arg<ExecuteTransactionRequest>()!
                        .TransactStatements.Select(s => Normalize(s.Statement)));
                return Task.FromResult(new ExecuteTransactionResponse());
            });
        return client;
    }
}

public sealed class RuntimeNamesJitContext(DbContextOptions<RuntimeNamesJitContext> options)
    : DbContext(options)
{
    public DbSet<RuntimeNamesJitItem> Items => Set<RuntimeNamesJitItem>();

    public DbSet<RuntimeNamesJitOther> Others => Set<RuntimeNamesJitOther>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuntimeNamesJitItem>(entity =>
        {
            entity.ToTable("placeholder-table").HasLogicalTableName("Items");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .HasGlobalSecondaryIndex("ByCategory", nameof(RuntimeNamesJitItem.Category))
                .HasSecondaryIndexName("placeholder-index");
        });

        modelBuilder.Entity<RuntimeNamesJitOther>(entity =>
        {
            entity.ToTable("other-placeholder-table").HasLogicalTableName("Others");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .HasGlobalSecondaryIndex("ByKind", nameof(RuntimeNamesJitOther.Kind))
                .HasSecondaryIndexName("other-placeholder-index");
        });
    }
}

public sealed class RuntimeNamesJitItem
{
    public string Pk { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

public sealed class RuntimeNamesJitOther
{
    public string Pk { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}

/// <summary>Two entity types share one table; only one of them declares the logical name.</summary>
public sealed class SharedTableContext(DbContextOptions<SharedTableContext> options)
    : DbContext(options)
{
    public DbSet<SharedFirst> First => Set<SharedFirst>();

    public DbSet<SharedSecond> Second => Set<SharedSecond>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedFirst>(entity =>
        {
            entity.ToTable("shared-design-table").HasLogicalTableName("Shared");
            entity.HasPartitionKey(item => item.Pk);
        });
        modelBuilder.Entity<SharedSecond>(entity =>
        {
            entity.ToTable("shared-design-table");
            entity.HasPartitionKey(item => item.Pk);
        });
    }
}

public sealed class SharedFirst
{
    public string Pk { get; set; } = string.Empty;
}

public sealed class SharedSecond
{
    public string Pk { get; set; } = string.Empty;
}

/// <summary>Two entity types share one table but declare different logical names.</summary>
public sealed class ConflictingLogicalNamesContext(
    DbContextOptions<ConflictingLogicalNamesContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedFirst>(entity =>
        {
            entity.ToTable("conflicting-table").HasLogicalTableName("Alpha");
            entity.HasPartitionKey(item => item.Pk);
        });
        modelBuilder.Entity<SharedSecond>(entity =>
        {
            entity.ToTable("conflicting-table").HasLogicalTableName("Beta");
            entity.HasPartitionKey(item => item.Pk);
        });
    }
}

/// <summary>Two different tables declare the same logical name.</summary>
public sealed class DuplicateLogicalNameContext(DbContextOptions<DuplicateLogicalNameContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedFirst>(entity =>
        {
            entity.ToTable("duplicate-table-a").HasLogicalTableName("Same");
            entity.HasPartitionKey(item => item.Pk);
        });
        modelBuilder.Entity<SharedSecond>(entity =>
        {
            entity.ToTable("duplicate-table-b").HasLogicalTableName("Same");
            entity.HasPartitionKey(item => item.Pk);
        });
    }
}

/// <summary>One table with two secondary indexes, for index-collision validation.</summary>
public sealed class TwoIndexContext(DbContextOptions<TwoIndexContext> options) : DbContext(options)
{
    public DbSet<TwoIndexRow> Rows => Set<TwoIndexRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TwoIndexRow>(entity =>
        {
            entity.ToTable("two-index-table").HasLogicalTableName("Two");
            entity.HasPartitionKey(row => row.Pk);
            entity.HasGlobalSecondaryIndex("ByA", nameof(TwoIndexRow.A));
            entity.HasGlobalSecondaryIndex("ByB", nameof(TwoIndexRow.B));
        });
}

public sealed class TwoIndexRow
{
    public string Pk { get; set; } = string.Empty;

    public string A { get; set; } = string.Empty;

    public string B { get; set; } = string.Empty;
}
