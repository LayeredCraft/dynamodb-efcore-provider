using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Design.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.AotTests;

/// <summary>
///     A model is built (JIT) or compiled with build-time physical resource names and declared
///     logical identities. Runtime physical names supplied through
///     <c>UseDynamo(o =&gt; o.RuntimeResourceNames(...))</c> must reach the initialized runtime model,
///     so that queries, index queries and writes all use them without regenerating the compiled
///     model.
/// </summary>
public partial class CompiledModelExecutionTests
{
    private const string PlaceholderTable = "placeholder-table";
    private const string PlaceholderIndex = "placeholder-index";
    private const string OtherTable = "other-placeholder-table";
    private const string OtherIndex = "other-placeholder-index";
    private const string RealTable = "real-table";
    private const string RealIndex = "real-index";

    private static void MapItems(DynamoRuntimeResourceNamesBuilder names)
        => names.Table("Items", RealTable).SecondaryIndex("Items", "ByCategory", RealIndex);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Compiled_model_without_runtime_mapping_uses_model_resource_names()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");
        var statements = new List<string>();

        await RunRuntimeNameScenarioAsync(compiled.Model, statements, map: null);

        statements.Should().Contain(s => s.Contains($"FROM \"{PlaceholderTable}\" WHERE"));
        statements.Should().Contain(
            s => s.Contains($"FROM \"{PlaceholderTable}\".\"{PlaceholderIndex}\""));
        statements.Should().Contain(s => s.Contains($"INSERT INTO \"{PlaceholderTable}\""));
        statements.Should().NotContain(s => s.Contains(RealTable) || s.Contains(RealIndex));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Compiled_model_applies_runtime_table_and_index_names_to_queries_and_writes()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");
        var statements = new List<string>();

        await RunRuntimeNameScenarioAsync(compiled.Model, statements, MapItems);

        // Normal (non-precompiled) base-table query.
        statements.Should().Contain(s => s.Contains($"FROM \"{RealTable}\" WHERE"));
        // Secondary-index query.
        statements.Should().Contain(s => s.Contains($"FROM \"{RealTable}\".\"{RealIndex}\""));
        // Write.
        statements.Should().Contain(s => s.Contains($"INSERT INTO \"{RealTable}\""));

        // Nothing may still target the build-time names of the remapped resources.
        statements.Should().NotContain(s => s.Contains($"\"{PlaceholderTable}\""));
        statements.Should().NotContain(s => s.Contains($"\"{PlaceholderIndex}\""));

        // A second table/index that has no mapping must not be affected.
        statements.Should().Contain(s => s.Contains($"FROM \"{OtherTable}\" WHERE"));
        statements.Should().Contain(
            s => s.Contains($"FROM \"{OtherTable}\".\"{OtherIndex}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Runtime_mapping_of_one_resource_does_not_affect_other_resources()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");
        var statements = new List<string>();

        await RunRuntimeNameScenarioAsync(
            compiled.Model,
            statements,
            names => names
                .Table("Others", "mapped-other-table")
                .SecondaryIndex("Others", "ByKind", "mapped-other-index"));

        statements.Should().Contain(s => s.Contains("FROM \"mapped-other-table\" WHERE"));
        statements.Should().Contain(
            s => s.Contains("FROM \"mapped-other-table\".\"mapped-other-index\""));
        // The first table and index keep the names they were compiled with.
        statements.Should().Contain(s => s.Contains($"FROM \"{PlaceholderTable}\" WHERE"));
        statements.Should().Contain(
            s => s.Contains($"FROM \"{PlaceholderTable}\".\"{PlaceholderIndex}\""));
        statements.Should().Contain(s => s.Contains($"INSERT INTO \"{PlaceholderTable}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Multiple_logical_tables_and_indexes_resolve_independently()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");
        var statements = new List<string>();

        await RunRuntimeNameScenarioAsync(
            compiled.Model,
            statements,
            names => names
                .Table("Items", RealTable)
                .Table("Others", "mapped-other-table")
                .SecondaryIndex("Items", "ByCategory", RealIndex)
                .SecondaryIndex("Others", "ByKind", "mapped-other-index"));

        statements.Should().Contain(s => s.Contains($"FROM \"{RealTable}\".\"{RealIndex}\""));
        statements.Should().Contain(
            s => s.Contains("FROM \"mapped-other-table\".\"mapped-other-index\""));
        statements.Should().NotContain(s => s.Contains($"\"{PlaceholderTable}\""));
        statements.Should().NotContain(s => s.Contains($"\"{OtherTable}\""));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Compiled_model_rejects_a_differently_configured_second_context()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");

        await RunRuntimeNameScenarioAsync(compiled.Model, [], MapItems);

        // An equal configuration shares the initialized model without error, even though it is a
        // different builder instance.
        await RunRuntimeNameScenarioAsync(
            compiled.Model,
            [],
            names => names.Table("Items", RealTable).SecondaryIndex("Items", "ByCategory", RealIndex));

        // A different configuration must fail loudly rather than silently use the first names.
        var different = () => RunRuntimeNameScenarioAsync(
            compiled.Model,
            [],
            names => names.Table("Items", "another-table"));
        (await different.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*already initialized*runtime resource-name configuration*");

        // So must no configuration at all.
        var noMapping = () => RunRuntimeNameScenarioAsync(compiled.Model, [], map: null);
        (await noMapping.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*already initialized*runtime resource-name configuration*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Unknown_logical_table_on_a_compiled_model_fails_fast()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");

        var act = () => RunRuntimeNameScenarioAsync(
            compiled.Model,
            [],
            names => names.Table("Itms", RealTable));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*logical table 'Itms'*Declared logical tables*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Compiled_model_keeps_logical_identity_and_never_bakes_runtime_names()
    {
        using var compiled = CompileModel<RuntimeNameContext>("CompiledRuntimeNames");

        // The logical table name and the EF index name are part of the model.
        compiled.GeneratedCode.Should().Contain("\"Dynamo:LogicalTableName\", \"Items\"");
        compiled.GeneratedCode.Should().Contain("name: \"ByCategory\"");
        // The design-time physical names are what the model is generated with.
        compiled.GeneratedCode.Should().Contain("\"Dynamo:TableName\", \"placeholder-table\"");
        compiled.GeneratedCode.Should().Contain(
            "\"Dynamo:SecondaryIndexName\", \"placeholder-index\"");
        // Runtime-only metadata is never generated into the compiled model.
        compiled.GeneratedCode.Should().NotContain("RuntimeSecondaryIndexName");
        compiled.GeneratedCode.Should().NotContain("AppliedRuntimeResourceNames");
        compiled.GeneratedCode.Should().NotContain("TableGroupName");
    }

    private static async Task RunRuntimeNameScenarioAsync(
        IModel? compiledModel,
        List<string> statements,
        Action<DynamoRuntimeResourceNamesBuilder>? map)
    {
        // One shared client keeps the number of EF internal service providers bounded: a provider is
        // created per distinct configuration, not per test.
        CurrentStatements.Value = statements;
        var builder = new DbContextOptionsBuilder<RuntimeNameContext>()
            .UseDynamo(configure =>
            {
                configure.DynamoDbClient(SharedRecordingClient.Value);
                if (map is not null)
                    configure.RuntimeResourceNames(map);
            })
            .ConfigureWarnings(warnings
                => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        if (compiledModel is not null)
            builder.UseModel(compiledModel);

        await using var context = new RuntimeNameContext(builder.Options);

        _ = await context.Items.Where(item => item.Pk == "1").ToListAsync();
        _ = await context.Items.Where(item => item.Category == "c").ToListAsync();
        _ = await context.Others.Where(item => item.Pk == "1").ToListAsync();
        _ = await context.Others.Where(item => item.Kind == "k").ToListAsync();

        context.Items.Add(new RuntimeNameItem { Pk = "2", Category = "c" });
        await context.SaveChangesAsync();
    }

    private static string Normalize(string statement)
        => Regex.Replace(statement, "\\s+", " ").Trim();

    private static readonly AsyncLocal<List<string>?> CurrentStatements = new();

    /// <summary>A minimal table catalog so table lifecycle calls can be exercised against the fake client.</summary>
    private sealed class FakeTables
    {
        private readonly Dictionary<string, CreateTableRequest> _tables = new(StringComparer.Ordinal);

        public List<CreateTableRequest> Created { get; } = [];

        public List<string> Deleted { get; } = [];

        public void Add(CreateTableRequest request)
        {
            Created.Add(request);
            _tables[request.TableName] = request;
        }

        public bool Remove(string tableName)
        {
            Deleted.Add(tableName);
            return _tables.Remove(tableName);
        }

        public TableDescription? Describe(string tableName)
            => _tables.TryGetValue(tableName, out var request)
                ? new TableDescription
                {
                    TableName = request.TableName,
                    TableStatus = TableStatus.ACTIVE,
                    KeySchema = request.KeySchema,
                    AttributeDefinitions = request.AttributeDefinitions,
                    BillingModeSummary = new BillingModeSummary { BillingMode = request.BillingMode },
                    GlobalSecondaryIndexes = request
                        .GlobalSecondaryIndexes
                        ?.Select(static index => new GlobalSecondaryIndexDescription
                        {
                            IndexName = index.IndexName,
                            IndexStatus = IndexStatus.ACTIVE,
                            KeySchema = index.KeySchema,
                            Projection = index.Projection
                        })
                        .ToList()
                }
                : null;
    }

    private static readonly AsyncLocal<FakeTables?> CurrentTables = new();

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
        client
            .DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var tableName = callInfo.Arg<DescribeTableRequest>()!.TableName;
                var description = CurrentTables.Value?.Describe(tableName);
                return description is null
                    ? Task.FromException<DescribeTableResponse>(
                        new ResourceNotFoundException($"Table '{tableName}' not found."))
                    : Task.FromResult(new DescribeTableResponse { Table = description });
            });
        client
            .CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<CreateTableRequest>()!;
                CurrentTables.Value!.Add(request);
                return Task.FromResult(
                    new CreateTableResponse { TableDescription = CurrentTables.Value.Describe(request.TableName) });
            });
        client
            .DeleteTableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var tableName = callInfo.Arg<string>()!;
                return CurrentTables.Value!.Remove(tableName)
                    ? Task.FromResult(new DeleteTableResponse { TableDescription = new TableDescription { TableName = tableName } })
                    : Task.FromException<DeleteTableResponse>(
                        new ResourceNotFoundException($"Table '{tableName}' not found."));
            });
        client
            .DeleteTableAsync(Arg.Any<DeleteTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var tableName = callInfo.Arg<DeleteTableRequest>()!.TableName;
                return CurrentTables.Value!.Remove(tableName)
                    ? Task.FromResult(new DeleteTableResponse { TableDescription = new TableDescription { TableName = tableName } })
                    : Task.FromException<DeleteTableResponse>(
                        new ResourceNotFoundException($"Table '{tableName}' not found."));
            });
        return client;
    }

    private static CompiledRuntimeNameModel CompileModel<TContext>(string modelNamespace)
        where TContext : DbContext, new()
    {
        using var designTimeContext = new TContext();
        var designTimeModel = designTimeContext.GetService<IDesignTimeModel>()!.Model;

        var typeMappingSource = designTimeContext.GetService<ITypeMappingSource>()!;
        var cSharpHelper = new CSharpHelper(typeMappingSource);
        var generator = new CSharpRuntimeModelCodeGenerator(
            new DynamoCSharpRuntimeAnnotationCodeGenerator(
                new CSharpRuntimeAnnotationCodeGeneratorDependencies(cSharpHelper)),
            cSharpHelper);
        var generatedFiles = generator.GenerateModel(
            designTimeModel,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(TContext),
                ModelNamespace = modelNamespace,
                ForNativeAot = true
            });

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Preview,
            documentationMode: DocumentationMode.Parse);
        var compilation = CSharpCompilation.Create(
            modelNamespace + "Assembly",
            generatedFiles.Select(file
                => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)),
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);
        var model = FindCompiledModelInstance(assembly);
        model.Should().NotBeNull();
        return new CompiledRuntimeNameModel(
            loadContext,
            model!,
            string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code)));
    }

    private sealed class CompiledRuntimeNameModel(
        AssemblyLoadContext loadContext,
        IModel model,
        string generatedCode) : IDisposable
    {
        public IModel Model { get; } = model;

        public string GeneratedCode { get; } = generatedCode;

        public void Dispose() => loadContext.Unload();
    }
}

public sealed class RuntimeNameContext : DbContext
{
    public RuntimeNameContext()
        : base(new DbContextOptionsBuilder<RuntimeNameContext>().UseDynamo().Options)
    {
    }

    public RuntimeNameContext(DbContextOptions<RuntimeNameContext> options)
        : base(options)
    {
    }

    public DbSet<RuntimeNameItem> Items => Set<RuntimeNameItem>();

    public DbSet<RuntimeNameOther> Others => Set<RuntimeNameOther>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuntimeNameItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, "placeholder-table").HasLogicalTableName("Items");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .HasGlobalSecondaryIndex("ByCategory", nameof(RuntimeNameItem.Category))
                .HasSecondaryIndexName("placeholder-index");
        });

        modelBuilder.Entity<RuntimeNameOther>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, "other-placeholder-table").HasLogicalTableName("Others");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .HasGlobalSecondaryIndex("ByKind", nameof(RuntimeNameOther.Kind))
                .HasSecondaryIndexName("other-placeholder-index");
        });
    }
}

public sealed class RuntimeNameItem
{
    public string Pk { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

public sealed class RuntimeNameOther
{
    public string Pk { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}
