using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Design.Internal;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Storage;
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
///     Compiles the generated compiled-model code (including primed collection mappings) with Roslyn,
///     loads it, and executes a real query and SaveChanges against the loaded model.
/// </summary>
public class CompiledModelExecutionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Generated_compiled_model_compiles_and_executes_round_trip_for_primed_collections()
    {
        var runtimeOptions = new DbContextOptionsBuilder<CompiledCollectionContext>()
            .UseDynamo()
            .Options;
        using var runtimeContext = new CompiledCollectionContext(runtimeOptions);
        var designTimeModel = runtimeContext.GetService<IDesignTimeModel>()!.Model;

        var typeMappingSource = runtimeContext.GetService<ITypeMappingSource>()!;
        var cSharpHelper = new CSharpHelper(typeMappingSource);
        var generator = new CSharpRuntimeModelCodeGenerator(
            new DynamoCSharpRuntimeAnnotationCodeGenerator(
                new CSharpRuntimeAnnotationCodeGeneratorDependencies(cSharpHelper)),
            cSharpHelper);
        var generatedFiles = generator.GenerateModel(
            designTimeModel,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(CompiledCollectionContext),
                ModelNamespace = CompiledModelNamespace,
                ForNativeAot = true
            });

        var generatedCode = string.Join(Environment.NewLine, generatedFiles.Select(f => f.Code));
        generatedCode.Should().Contain("PrimeListMapping<");
        generatedCode.Should().Contain("PrimeSetMapping<HashSet<int>, int>(");
        generatedCode
            .Should()
            .Contain("PrimeDictionaryMapping<Dictionary<string, decimal>, decimal>(");
        generatedCode.Should().Contain("PrimeListMapping<List<int?>, int?>(");
        generatedCode.Should().Contain("PrimeListMapping<List<Guid>, Guid>(");

        // Runtime table-group names are recomputed by DynamoModelRuntimeInitializer, never
        // serialized into compiled models.
        generatedCode.Should().NotContain("TableGroupName");
        generatedCode.Should().Contain("(DynamoTypeMapping)(");

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Preview,
            documentationMode: DocumentationMode.Parse);
        var compilation = CSharpCompilation.Create(
            "CompiledModelRoundTripAssembly",
            generatedFiles.Select(file
                => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)),
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var compiledModel = FindCompiledModelInstance(assembly);
            compiledModel.Should().NotBeNull();

            var store = new Dictionary<string, Dictionary<string, AttributeValue>>();
            var fakeClient = CreateFakeClient(store);

            var options = new DbContextOptionsBuilder<CompiledCollectionContext>()
                .UseDynamo(configure => configure.DynamoDbClient(fakeClient))
                .UseModel(compiledModel!)
                .Options;

            var expected = new CompiledCollectionItem(
                "PRIMED#1",
                [1, 2, 3],
                [7, 11],
                new Dictionary<string, decimal> { ["tax"] = 1.25m, ["fee"] = 0.5m },
                [null, 42, null],
                [new("0f8fad5b-d9cb-469f-a165-70867728950e")]);

            await using (var context = new CompiledCollectionContext(options))
            {
                // Normal and compiled models must resolve equivalent runtime table metadata.
                var dynamicTableModel = runtimeContext.Model.GetDynamoRuntimeTableModel();
                var compiledTableModel = context.Model.GetDynamoRuntimeTableModel();
                dynamicTableModel.Should().NotBeNull();
                compiledTableModel.Should().NotBeNull();
                compiledTableModel!
                    .Tables
                    .Keys
                    .Should()
                    .BeEquivalentTo(dynamicTableModel!.Tables.Keys);
                foreach (var (tableName, dynamicTable) in dynamicTableModel.Tables)
                {
                    var compiledTable = compiledTableModel.Tables[tableName];
                    compiledTable
                        .RootEntityTypes
                        .Select(type => type.Name)
                        .Should()
                        .Equal(dynamicTable.RootEntityTypes.Select(type => type.Name));
                    compiledTable
                        .SourcesByQueryEntityTypeName
                        .Keys
                        .Should()
                        .BeEquivalentTo(dynamicTable.SourcesByQueryEntityTypeName.Keys);
                }

                context.Items.Add(expected);
                await context.SaveChangesAsync();
            }

            store.Should().ContainKey("PRIMED#1");
            var storedItem = store["PRIMED#1"];

            // Pin the raw wire shape so symmetric serialize/deserialize bugs cannot pass silently.
            storedItem["scores"]
                .L
                .Select(value => value.N)
                .Should()
                .BeEquivalentTo(["1", "2", "3"], options => options.WithStrictOrdering());
            storedItem["flags"]
                .NS
                .Should()
                .BeEquivalentTo(["7", "11"], options => options.WithStrictOrdering());
            var charges = storedItem["charges"].M;
            charges.Should().HaveCount(2);
            charges["tax"].N.Should().Be("1.25");
            charges["fee"].N.Should().Be("0.5");
            var optionalScores = storedItem["optionalScores"].L;
            optionalScores.Should().HaveCount(3);
            optionalScores[0].NULL.Should().BeTrue();
            optionalScores[1].N.Should().Be("42");
            optionalScores[2].NULL.Should().BeTrue();
            var convertedIds = storedItem["convertedIds"].L;
            convertedIds
                .Select(value => value.S)
                .Should()
                .BeEquivalentTo(
                    ["0f8fad5b-d9cb-469f-a165-70867728950e"],
                    options => options.WithStrictOrdering());

            // Decoy row makes the pk filter in the SELECT load-bearing: if filtering breaks, the
            // SingleAsync below fails on count instead of silently returning everything.
            store["DECOY#1"] = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "DECOY#1" }
            };

            CompiledCollectionItem actual;
            await using (var context = new CompiledCollectionContext(options))
            {
                var pk = "PRIMED#1";
                actual = await context.Items.SingleAsync(item => item.Pk == pk);
            }

            actual.Should().BeEquivalentTo(expected);

            var mutableExpected = new CompiledMutableCollectionItem
            {
                Pk = "MUTABLE#1",
                Charges = new Dictionary<string, decimal> { ["tax"] = 2.5m },
                Flags = [3, 9],
                Scores = [4]
            };

            await using (var context = new CompiledCollectionContext(options))
            {
                context.MutableItems.Add(mutableExpected);
                await context.SaveChangesAsync();
            }

            CompiledMutableCollectionItem mutableActual;
            await using (var context = new CompiledCollectionContext(options))
            {
                var pk = "MUTABLE#1";
                mutableActual = await context.MutableItems.SingleAsync(item => item.Pk == pk);
            }

            mutableActual.Should().BeEquivalentTo(mutableExpected);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private const string CompiledModelNamespace = "CompiledModelRoundTrip";

    private static string Describe(AttributeValue value)
    {
        if (value.NULL == true)
            return "NULL";
        if (value.N is not null)
            return $"N:{value.N}";
        if (value.S is not null)
            return $"S:{value.S}";
        if (value.NS is { Count: > 0 })
            return $"NS[{string.Join(",", value.NS)}]";
        if (value.L is not null)
            return $"L[{string.Join(",", value.L.Select(Describe))}]";
        if (value.M is not null)
            return
                $"M{{{string.Join(",", value.M.Select(kvp => $"{kvp.Key}={Describe(kvp.Value)}"))}}}";
        return "?";
    }

    private static IModel? FindCompiledModelInstance(Assembly assembly)
    {
        var modelType = assembly
            .GetTypes()
            .SingleOrDefault(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Any(property
                    => property.Name == "Instance"
                    && property.CanRead
                    && typeof(IModel).IsAssignableFrom(property.PropertyType)));
        if (modelType is null)
            return null;

        return (IModel?)modelType.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
    }

    internal static IAmazonDynamoDB CreateFakeClient(
        Dictionary<string, Dictionary<string, AttributeValue>> store)
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ExecuteStatementRequest>()!;
                return Task.FromResult(HandleExecuteStatement(request, store));
            });

        client
            .ExecuteTransactionAsync(
                Arg.Any<ExecuteTransactionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ExecuteTransactionRequest>()!;
                foreach (var statement in request.TransactStatements ?? [])
                    HandleWriteStatement(statement.Statement, statement.Parameters ?? [], store);

                return Task.FromResult(new ExecuteTransactionResponse());
            });

        return client;
    }

    private static ExecuteStatementResponse HandleExecuteStatement(
        ExecuteStatementRequest request,
        Dictionary<string, Dictionary<string, AttributeValue>> store)
    {
        var statement = request.Statement.TrimStart();
        if (statement.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            var items = store.Values.ToList();
            var parameters = request.Parameters ?? [];

            // Map parameter positions by counting '?' placeholders before the predicate so
            // translator-side parameter reordering cannot silently corrupt the fake filtering.
            string? FilterByPlaceholder(string predicate)
            {
                var index = statement.IndexOf(predicate, StringComparison.Ordinal);
                if (index < 0)
                    return null;
                var placeholderIndex = statement
                    .Substring(0, index)
                    .Count(character => character == '?');
                return placeholderIndex < parameters.Count ? parameters[placeholderIndex].S : null;
            }

            var expectedPk = FilterByPlaceholder("\"pk\" = ?")
                ?? (PkLiteralRegex.Match(statement) is { Success: true } literalMatch
                    ? literalMatch.Groups["pk"].Value.Replace("''", "'")
                    : null);
            if (expectedPk is not null)
            {
                items = items
                    .Where(item => item.TryGetValue("pk", out var pk) && pk.S == expectedPk)
                    .ToList();

                // `contains("externalIds", ...)` predicates are applied client-side so a wrong
                // predicate translation cannot silently return every row.
                var expectedId = FilterByPlaceholder("contains(\"externalIds\"")
                    ?? (ExternalIdsLiteralRegex.Match(statement) is { Success: true } idMatch
                        ? idMatch.Groups["id"].Value.Replace("''", "'")
                        : null);
                if (expectedId is not null)
                    items = items
                        .Where(item => item.TryGetValue("externalIds", out var ids)
                            && ids.L.Any(id => id.S == expectedId))
                        .ToList();
            }
            else if (statement.Contains("\"pk\" IN", StringComparison.Ordinal))
            {
                var expectedPks = parameters.Select(parameter => parameter.S).ToHashSet();
                items = items
                    .Where(item => item.TryGetValue("pk", out var pk)
                        && pk.S is not null
                        && expectedPks.Contains(pk.S))
                    .ToList();
            }

            return new ExecuteStatementResponse { Items = items };
        }

        HandleWriteStatement(statement, request.Parameters ?? [], store);
        return new ExecuteStatementResponse();
    }

    private static void HandleWriteStatement(
        string statement,
        IReadOnlyList<AttributeValue> parameters,
        Dictionary<string, Dictionary<string, AttributeValue>> store)
    {
        if (!statement.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Fake client only supports INSERT write statements, got '{statement}'.");

        var names = PlaceholderNameRegex.Matches(statement);
        if (names.Count != parameters.Count)
            throw new InvalidOperationException(
                $"Statement '{statement}' has {names.Count} placeholders but "
                + $"{parameters.Count} parameters.");

        var item = new Dictionary<string, AttributeValue>();
        for (var index = 0; index < names.Count; index++)
            item[names[index].Groups["name"].Value] = parameters[index];

        store[item["pk"].S] = item;
    }

    private static readonly Regex PlaceholderNameRegex =
        new(@"'(?<name>[^']+)'\s*:\s*\?", RegexOptions.Compiled);

    private static readonly Regex PkLiteralRegex =
        new("\"pk\"\\s*=\\s*'(?<pk>[^']*)'", RegexOptions.Compiled);

    private static readonly Regex ExternalIdsLiteralRegex = new(
        "contains\\(\"externalIds\",\\s*'(?<id>[^']*)'\\)",
        RegexOptions.Compiled);

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
        => CandidateAssemblyPaths()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                try
                {
                    return MetadataReference.CreateFromFile(path);
                }
                catch (BadImageFormatException)
                {
                    // Skip native/non-managed DLLs that cannot serve as metadata references.
                    return null;
                }
            })
            .Where(reference => reference is not null)
            .Cast<MetadataReference>()
            .ToArray();

    private static IEnumerable<string> CandidateAssemblyPaths()
    {
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (tpa is not null)
            foreach (var path in tpa.Split(Path.PathSeparator))
                yield return path;

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            yield return path;
    }

    private static (AssemblyLoadContext LoadContext, Assembly Assembly) EmitAndLoad(
        Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        emitResult
            .Success
            .Should()
            .BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));

        stream.Position = 0;
        var loadContext = new AssemblyLoadContext(
            nameof(CompiledModelExecutionTests),
            isCollectible: true);
        return (loadContext, loadContext.LoadFromStream(stream));
    }

    private static void AssertCompilationSucceeded(Compilation compilation)
    {
        var errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length == 0)
            return;

        var details = errors.Select(error =>
        {
            var line = error.Location.GetLineSpan().StartLinePosition.Line;
            var lines = error.Location.SourceTree?.GetText().Lines;
            var sourceLine = lines is not null && line < lines.Count
                ? lines[line].ToString()
                : string.Empty;
            return $"{error}{Environment.NewLine}{sourceLine}";
        });
        throw new InvalidOperationException(string.Join(Environment.NewLine, details));
    }
}

public sealed class CompiledCollectionContext(DbContextOptions<CompiledCollectionContext> options)
    : DbContext(options)
{
    public DbSet<CompiledCollectionItem> Items => Set<CompiledCollectionItem>();

    public DbSet<CompiledMutableCollectionItem> MutableItems
        => Set<CompiledMutableCollectionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompiledCollectionItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, "CompiledCollectionItems");
            entity.HasPartitionKey(item => item.Pk);
            entity
                .PrimitiveCollection(item => item.ConvertedIds)
                .ElementType(e => e.HasConversion<string>());
        });

        modelBuilder.Entity<CompiledMutableCollectionItem>(entity =>
        {
            DynamoEntityTypeBuilderExtensions.ToTable(entity, "CompiledCollectionItems");
            entity.HasPartitionKey(item => item.Pk);
        });
    }
}

public sealed record CompiledCollectionItem(
    string Pk,
    List<int> Scores,
    HashSet<int> Flags,
    Dictionary<string, decimal> Charges,
    List<int?> OptionalScores,
    List<Guid> ConvertedIds);

public sealed class CompiledMutableCollectionItem
{
    public string Pk { get; set; } = string.Empty;

    public Dictionary<string, decimal> Charges { get; set; } = [];

    public HashSet<int> Flags { get; set; } = [];

    public List<int> Scores { get; set; } = [];
}
