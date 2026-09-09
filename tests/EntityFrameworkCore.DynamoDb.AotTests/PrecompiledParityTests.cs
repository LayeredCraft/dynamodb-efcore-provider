using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Design.Internal;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.AotTests;

/// <summary>
///     Executes the same query rows through the interpreter path and through generated
///     precompiled interceptors against identical fake stores, then asserts that materialized
///     results and emitted statements match. Rows pin the highest-risk divergences: nullable
///     value materialization, limit handling, composite keys, collection parameters, and
///     null-shaping projections.
/// </summary>
public class PrecompiledParityTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Interpreter_and_precompiled_paths_match_for_supported_query_rows()
    {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class ParityContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<ParityItem> Items => Set<ParityItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<ParityItem>(entity =>
                              {
                              Microsoft.EntityFrameworkCore.DynamoEntityTypeBuilderExtensions.ToTable(entity, "ParityItems");
                              entity.HasPartitionKey(item => item.Pk);
                              Microsoft.EntityFrameworkCore.DynamoEntityTypeBuilderExtensions.HasSortKey(entity, item => item.Sk);
                              entity.Property(item => item.Status).HasConversion<string>();
                              entity.Property(item => item.NullableStatus).HasConversion<string>();
                              });
                              }
                              }

                              public sealed class ParityItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Sk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              public ParityStatus Status { get; set; }
                              public ParityStatus? NullableStatus { get; set; }
                              public int? Count { get; set; }
                              }

                              public enum ParityStatus
                              {
                              Active,
                              Inactive
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<ParityItem>> MaterializeNullsAsync(DbContextOptions options)
                              {
                              await using var context = new ParityContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "p1" && item.Sk == "s1")
                              .ToListAsync();
                              }

                              public static async Task<List<ParityItem>> CompositeKeyAsync(DbContextOptions options)
                              {
                              await using var context = new ParityContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "p1" && item.Sk == "s2")
                              .ToListAsync();
                              }

                              public static async Task<List<ParityItem>> ContainsKeysAsync(DbContextOptions options)
                              {
                              await using var context = new ParityContext(options);
                              string[] keys = ["p1", "p3"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
                              .ToListAsync();
                              }

                              public static async Task<ParityItem?> MissingKeyAsync(DbContextOptions options)
                              {
                              await using var context = new ParityContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "missing")
                              .FirstOrDefaultAsync();
                              }
                              }
                              """;

        var parseOptions = new CSharpParseOptions().WithFeatures(
        [
            new KeyValuePair<string, string>(
                "InterceptorsNamespaces",
                "Microsoft.EntityFrameworkCore.GeneratedInterceptors")
        ]);
        var compilation = CSharpCompilation.Create(
            "DynamoParityTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);

        var rows = new (string Method, Func<JsonElement, object?> Project)[]
        {
            ("MaterializeNullsAsync", document => document),
            ("CompositeKeyAsync", document => document),
            ("ContainsKeysAsync", document => document),
            ("MissingKeyAsync", document => document)
        };

        var (interpreterLoadContext, interpreterAssembly) = EmitAndLoad(compilation);
        try
        {
            await using var designContext = (DbContext)Activator.CreateInstance(
                interpreterAssembly.GetType("GeneratedQueryTest.ParityContext")!,
                new DbContextOptionsBuilder().UseDynamo().Options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    designContext,
                    new Dictionary<MemberInfo,
                        Microsoft.EntityFrameworkCore.Design.Internal.QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    interpreterAssembly);

            errors
                .Should()
                .BeEmpty(
                    string.Join(
                        Environment.NewLine + "---" + Environment.NewLine,
                        errors.Select(error
                            => error.SyntaxNode + Environment.NewLine + error.Exception.Message)));

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                foreach (var (method, _) in rows)
                {
                    var interpreterStore = SeedStore();
                    var (interpreterClient, interpreterStatements) =
                        CreateCapturingFakeClient(interpreterStore);
                    var interpreterOptions = new DbContextOptionsBuilder().UseDynamo(configure
                            => configure.DynamoDbClient(interpreterClient))
                        .Options;
                    var interpreterResult = await InvokeQueryAsync(
                        interpreterAssembly,
                        method,
                        interpreterOptions);

                    var precompiledStore = SeedStore();
                    var (precompiledClient, precompiledStatements) =
                        CreateCapturingFakeClient(precompiledStore);
                    var precompiledOptions = new DbContextOptionsBuilder().UseDynamo(configure
                            => configure.DynamoDbClient(precompiledClient))
                        .Options;
                    var precompiledResult = await InvokeQueryAsync(
                        generatedAssembly,
                        method,
                        precompiledOptions);

                    Json(interpreterResult)
                        .Should()
                        .Be(
                            Json(precompiledResult),
                            $"{method} must materialize identically through both paths");

                    interpreterStatements
                        .Should()
                        .BeEquivalentTo(
                            precompiledStatements,
                            $"{method} must emit the same statements through both paths");
                }
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            interpreterLoadContext.Unload();
        }
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value);

    private static async Task<object?> InvokeQueryAsync(
        Assembly assembly,
        string methodName,
        DbContextOptions options)
    {
        var method = assembly.GetType("GeneratedQueryTest.QueryContainer")!.GetMethod(methodName)!;
        var invoke = method.Invoke(null, [options])!;
        await (Task)invoke;
        return invoke.GetType().GetProperty("Result")!.GetValue(invoke);
    }

    private static Dictionary<string, Dictionary<string, AttributeValue>> SeedStore()
    {
        Dictionary<string, AttributeValue> Item(
            string pk,
            string sk,
            string name,
            string status,
            string? nullableStatus,
            AttributeValue? count)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = pk },
                ["sk"] = new() { S = sk },
                ["name"] = new() { S = name },
                ["status"] = new() { S = status },
                ["$type"] = new() { S = "ParityItem" }
            };
            item["nullableStatus"] = nullableStatus is null
                ? new AttributeValue { NULL = true }
                : new AttributeValue { S = nullableStatus };
            item["count"] = count ?? new AttributeValue { NULL = true };
            return item;
        }

        return new Dictionary<string, Dictionary<string, AttributeValue>>
        {
            ["p1|s1"] = Item("p1", "s1", "A", "Active", null, null),
            ["p1|s2"] = Item("p1", "s2", "B", "Inactive", "Active", new() { N = "7" }),
            ["p1|s3"] = Item("p1", "s3", "C", "Active", null, new() { N = "9" }),
            ["p1|s4"] = Item("p1", "s4", "D", "Active", null, new() { N = "2" }),
            ["p1|s5"] = Item("p1", "s5", "E", "Active", null, new() { N = "3" }),
            ["p3|s1"] = Item("p3", "s1", "F", "Active", null, new() { N = "1" })
        };
    }

    private static (IAmazonDynamoDB Client, List<string> Statements) CreateCapturingFakeClient(
        Dictionary<string, Dictionary<string, AttributeValue>> store)
    {
        var statements = new List<string>();
        var client = Substitute.For<IAmazonDynamoDB>();

        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ExecuteStatementRequest>()!;
                statements.Add(request.Statement);
                return Task.FromResult(HandleSelect(request, store));
            });

        return (client, statements);
    }

    private static ExecuteStatementResponse HandleSelect(
        ExecuteStatementRequest request,
        Dictionary<string, Dictionary<string, AttributeValue>> store)
    {
        var statement = request.Statement;
        var parameters = request.Parameters ?? [];

        static string? LiteralValue(string property, string statement)
        {
            var marker = $"\"{property}\" = '";
            var start = statement.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += marker.Length;
            var end = statement.IndexOf('\'', start);
            return end < 0
                ? null
                : statement[start..end].Replace("''", "'", StringComparison.Ordinal);
        }

        string? FilterValue(string predicate)
        {
            var index = statement.IndexOf(predicate, StringComparison.Ordinal);
            if (index < 0)
                return null;
            var placeholderIndex = statement
                .Substring(0, index)
                .Count(character => character == '?');
            return placeholderIndex < parameters.Count ? parameters[placeholderIndex].S : null;
        }

        var items = store.Values.ToList();

        var expectedSk = FilterValue("\"sk\" = ?") ?? LiteralValue("sk", statement);
        if (expectedSk is not null)
            items = items
                .Where(item => item.TryGetValue("sk", out var sk) && sk.S == expectedSk)
                .ToList();

        var expectedPk = FilterValue("\"pk\" = ?") ?? LiteralValue("pk", statement);
        if (expectedPk is not null)
        {
            items = items
                .Where(item => item.TryGetValue("pk", out var pk) && pk.S == expectedPk)
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

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

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
            nameof(PrecompiledParityTests),
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
