using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Design.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.AotTests;

public class PrecompiledQueryGenerationTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Generated_runtime_resolves_an_inherited_property_once()
    {
        using var context = new InheritanceContext(
            new DbContextOptionsBuilder<InheritanceContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);
        var property = ResolveProperty(
            context.Model,
            typeof(BaseItem).FullName!,
            nameof(BaseItem.Status));

        property.DeclaringType.Name.Should().Be(typeof(BaseItem).FullName);
        property.Name.Should().Be(nameof(BaseItem.Status));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Generated_runtime_reports_missing_inherited_property()
    {
        using var context = new InheritanceContext(
            new DbContextOptionsBuilder<InheritanceContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);

        var action = () => ResolveProperty(context.Model, typeof(BaseItem).FullName!, "Missing");

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*BaseItem.Missing*was not found*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Generated_runtime_resolves_properties_by_full_type_name_when_short_names_collide()
    {
        using var context = new DuplicateNameContext(
            new DbContextOptionsBuilder<DuplicateNameContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);

        var first = ResolveProperty(
            context.Model,
            typeof(First.Widget).FullName!,
            nameof(First.Widget.Pk));
        first.DeclaringType.Name.Should().Be(typeof(First.Widget).FullName);

        var second = ResolveProperty(
            context.Model,
            typeof(Second.Widget).FullName!,
            nameof(Second.Widget.Pk));
        second.DeclaringType.Name.Should().Be(typeof(Second.Widget).FullName);

        // Full-name identity must be required: a short-name lookup is ambiguous and must not
        // silently match one of the colliding entity types.
        var action = () => ResolveProperty(context.Model, "Widget", nameof(First.Widget.Pk));
        action.Should().Throw<InvalidOperationException>().WithMessage("*Widget.Pk*was not found*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Generated_interceptor_compiles_and_upstream_executor_template_matches_rewrite_contract()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;
                              using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.UsePropertyAccessMode(PropertyAccessMode.PreferProperty);
                              modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              entity.Property(item => item.Status).HasConversion<string>();
                              });
                              }
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              public TestStatus Status { get; set; }
                              public List<System.Guid> ExternalIds { get; set; } = [];
                              public int Count { get; set; }
                              }

                              public enum TestStatus
                              {
                              Active
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<string>> Execute(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              var pk = "tenant-1";
                              return await context.Items
                              .Where(item => item.Pk == pk)
                              .Select(item => item.Name)
                              .ToListAsync();
                              }

                              public static async Task<List<TestItem>> ExecuteEntities(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              string[] keys = ["tenant-1", "tenant-2"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
                              .ToListAsync();
                              }

                              public static async Task<List<TestItem>> ExecuteTagContains(
                               DbContextOptions options)
                               {
                               await using var context = new TestContext(options);
                               var id = new System.Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
                               return await context.Items
                               .Where(item
                               => item.Pk == "tenant-1" && item.ExternalIds.Contains(id))
                               .ToListAsync();
                               }

                              public static async Task<TestStatus> ExecuteConvertedProjection(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Status)
                              .FirstAsync();
                              }

                              public static async Task<int> ExecuteUpdate(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              var pk = "tenant-1";
                              return await context.Items
                              .Where(item => item.Pk == pk)
                              .ExecuteUpdateAsync(setters
                              => setters.SetProperty(item => item.Name, "updated"));
                              }

                              public static async Task<int> ExecuteUpdateSelfReference(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              var pk = "tenant-1";
                              return await context.Items
                              .Where(item => item.Pk == pk)
                              .ExecuteUpdateAsync(setters
                              => setters.SetProperty(item => item.Count, item => item.Count + 1));
                              }

                              public static async Task<int> ExecuteDelete(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              var pk = "tenant-3";
                              return await context.Items
                              .Where(item => item.Pk == pk)
                              .ExecuteDeleteAsync();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var upstreamErrors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var upstreamGeneratedFiles =
                new PrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    upstreamErrors,
                    new HashSet<string>(),
                    assembly);

            upstreamErrors.Should().BeEmpty();
            upstreamGeneratedFiles.Should().NotBeEmpty();
            string
                .Join(Environment.NewLine, upstreamGeneratedFiles.Select(file => file.Code))
                .Should()
                .Contain(RelationalExecutorPreamble);

            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
            generatedCode.Should().Contain("CreateQueryTemplate");
            generatedCode.Should().Contain("CreateUpdateTemplate");
            generatedCode.Should().Contain("CreateUpdateExecutorAsync");
            generatedCode.Should().Contain("CreateValueReader");
            generatedCode.Should().Contain("InterceptsLocationAttribute(1,");
            // Contains over a native primitive collection must bind the element mapping to the
            // owning property (element depth 1) so runtime resolution never hits the AOT-unsafe
            // FindMapping fallback.
            generatedCode.Should().Contain("\"ExternalIds\", 1)");
            generatedCode.Should().NotContain("SelectExpressionJson");
            generatedCode.Should().NotContain("RelationalMaterializerLiftableConstantContext");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            // Interceptor binding failures surface as warnings, not errors: an unbound
            // interceptor silently falls back to JIT translation and the execution assertions
            // below would pass without ever exercising the precompiled path.
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty(
                    string.Join(
                        Environment.NewLine,
                        generatedCompilation
                            .GetDiagnostics()
                            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                            .Select(diagnostic => diagnostic.ToString())));

            // Execute the generated interceptor in-process against a fake client so a
            // wrong-but-self-consistent rewrite cannot pass the shape assertions above.
            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                Dictionary<string, AttributeValue> Item(
                    string pk,
                    string name,
                    List<AttributeValue>? externalIds = null)
                    => new()
                    {
                        ["pk"] = new() { S = pk },
                        ["name"] = new() { S = name },
                        ["status"] = new() { S = "Active" },
                        ["$type"] = new() { S = "TestItem" },
                        ["externalIds"] = new() { L = externalIds ?? [] },
                        ["count"] = new() { N = "0" }
                    };

                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    ["tenant-1"] =
                        Item(
                            "tenant-1",
                            "name-1",
                            [new() { S = "0f8fad5b-d9cb-469f-a165-70867728950e" }]),
                    ["tenant-2"] = Item("tenant-2", "name-2"),
                    ["tenant-3"] = Item("tenant-3", "name-3")
                };
                var fakeOptions = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(configure
                        => configure.DynamoDbClient(
                            CompiledModelExecutionTests.CreateFakeClient(store)))
                    .Options;

                var names =
                    (List<string>)(await InvokeQueryAsync(generatedAssembly, "Execute", fakeOptions)
                        ?? throw new InvalidOperationException("Execute returned null."));
                names.Should().BeEquivalentTo(["name-1"], options => options.WithStrictOrdering());

                var entities =
                    ((System.Collections.IEnumerable)(await InvokeQueryAsync(
                            generatedAssembly,
                            "ExecuteEntities",
                            fakeOptions)
                        ?? throw new InvalidOperationException("ExecuteEntities returned null.")))
                    .Cast<object>()
                    .ToList();
                entities
                    .Select(item => (string)item.GetType().GetProperty("Pk")!.GetValue(item)!)
                    .Should()
                    .BeEquivalentTo(["tenant-1", "tenant-2"]);

                // ExecuteTagContains is asserted at generation time only: materializing or
                // even projecting through the List<Guid> property pulls in the generated
                // field-read accessor that NativeAOT does not support yet.
                var status =
                    (int)(await InvokeQueryAsync(
                            generatedAssembly,
                            "ExecuteConvertedProjection",
                            fakeOptions)
                        ?? throw new InvalidOperationException(
                            "ExecuteConvertedProjection returned null."));
                status.Should().Be((int)TestStatus.Active);

                var affected =
                    (int)(await InvokeQueryAsync(generatedAssembly, "ExecuteUpdate", fakeOptions)
                        ?? throw new InvalidOperationException("ExecuteUpdate returned null."));
                affected.Should().Be(1);
                store["tenant-1"]["name"].S.Should().Be("updated");

                var selfReferenceAffected =
                    (int)(await InvokeQueryAsync(
                            generatedAssembly,
                            "ExecuteUpdateSelfReference",
                            fakeOptions)
                        ?? throw new InvalidOperationException(
                            "ExecuteUpdateSelfReference returned null."));
                selfReferenceAffected.Should().Be(1);
                store["tenant-1"]["count"].N.Should().Be("1");

                var deleteAffected =
                    (int)(await InvokeQueryAsync(generatedAssembly, "ExecuteDelete", fakeOptions)
                        ?? throw new InvalidOperationException("ExecuteDelete returned null."));
                deleteAffected.Should().Be(1);
                store.Should().NotContainKey("tenant-3");
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Generated_interceptor_inlines_unconverted_scalar_reads_and_keeps_converter_fallback()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;
                              using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                              namespace GeneratedQueryTest;

                              public sealed class ScalarContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<ScalarItem> Items => Set<ScalarItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<ScalarItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              entity.Property(item => item.Status).HasConversion<string>();
                              entity.Property(item => item.Tag).HasConversion(new TrimConverter());
                              entity.Property(item => item.Score).HasConversion(new NullableScoreConverter());
                              });
                              }
                              }

                              public sealed class ScalarItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              public int Count { get; set; }
                              public int? OptionalCount { get; set; }
                              public bool Active { get; set; }
                              public TestStatus Kind { get; set; }
                              public TestStatus? OptionalKind { get; set; }
                              public TestStatus Status { get; set; }
                              public string Tag { get; set; } = "";
                              public int? Score { get; set; }
                              }

                              public sealed class TrimConverter : ValueConverter<string, string>
                              {
                              public TrimConverter()
                              : base(value => value, value => value.Trim())
                              {
                              }
                              }

                              public sealed class NullableScoreConverter : ValueConverter<int?, string>
                              {
                              public NullableScoreConverter()
                              : base(
                              value => value.HasValue ? value.Value.ToString() : null,
                              value => value == null ? null : int.Parse(value),
                              convertsNulls: true)
                              {
                              }
                              }

                              public enum TestStatus
                              {
                              Active
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<string>> NameQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Name)
                              .ToListAsync();
                              }

                              public static async Task<List<int>> CountQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Count)
                              .ToListAsync();
                              }

                              public static async Task<List<int?>> OptionalCountQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.OptionalCount)
                              .ToListAsync();
                              }

                              public static async Task<List<bool>> ActiveQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Active)
                              .ToListAsync();
                              }

                              public static async Task<List<TestStatus>> KindQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Kind)
                              .ToListAsync();
                              }

                              public static async Task<List<TestStatus?>> OptionalKindQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.OptionalKind)
                              .ToListAsync();
                              }

                              public static async Task<List<TestStatus>> StatusQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Status)
                              .ToListAsync();
                              }

                              public static async Task<List<string?>> TagQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              string[] keys = ["tenant-1", "tenant-2"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
                              .Select(item => item.Tag)
                              .ToListAsync();
                              }

                              public static async Task<List<int?>> ScoreQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              string[] keys = ["tenant-1", "tenant-2"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
                              .Select(item => item.Score)
                              .ToListAsync();
                              }

                              public static async Task<List<TestStatus>> StatusMultiQuery(DbContextOptions options)
                              {
                              await using var context = new ScalarContext(options);
                              string[] keys = ["tenant-1", "tenant-2"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
                              .Select(item => item.Status)
                              .ToListAsync();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.ScalarContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));

            // Unconverted scalars read through the typed static ReadScalar path. Converter-backed
            // scalars retain the query-lifetime reader fallback, avoiding per-row codec creation.
            generatedCode.Should().Contain("CreateValueReader<");
            generatedCode.Should().Contain("DynamoGeneratedQueryRuntime.ReadScalar<");
            generatedCode.Should().NotContain("HasValue(item[");

            // Execute the generated converted-scalar reads against a fake client, including
            // NULL and missing wire values, to pin runtime parity of the fallback path.
            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(
                compilation.AddSyntaxTrees(
                    generatedFiles.Select(file
                        => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path))));
            try
            {
                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    // Tag present (converted through the custom converter), Score NULL.
                    ["tenant-1"] = new()
                    {
                        ["pk"] = new() { S = "tenant-1" },
                        ["$type"] = new() { S = "ScalarItem" },
                        ["kind"] = new() { N = "0" },
                        ["optionalKind"] = new() { N = "0" },
                        ["status"] = new() { S = "Active" },
                        ["tag"] = new() { S = " x " },
                        ["score"] = new() { NULL = true }
                    },
                    // Tag and Score missing entirely, Status present.
                    ["tenant-2"] = new()
                    {
                        ["pk"] = new() { S = "tenant-2" },
                        ["$type"] = new() { S = "ScalarItem" },
                        ["status"] = new() { S = "Active" }
                    }
                };
                var fakeOptions = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(configure
                        => configure.DynamoDbClient(
                            CompiledModelExecutionTests.CreateFakeClient(store)))
                    .Options;

                var tags =
                    (List<string?>)(await InvokeQueryAsync(
                            generatedAssembly,
                            "TagQuery",
                            fakeOptions)
                        ?? throw new InvalidOperationException("TagQuery returned null."));
                tags.Should().Equal("x", null);

                var scores =
                    (List<int?>)(await InvokeQueryAsync(
                            generatedAssembly,
                            "ScoreQuery",
                            fakeOptions)
                        ?? throw new InvalidOperationException("ScoreQuery returned null."));
                scores
                    .Should()
                    .BeEquivalentTo(
                        [default(int?), default(int?)],
                        options => options.WithStrictOrdering());

                var statuses =
                    ((System.Collections.IEnumerable)(await InvokeQueryAsync(
                            generatedAssembly,
                            "StatusMultiQuery",
                            fakeOptions)
                        ?? throw new InvalidOperationException("StatusMultiQuery returned null.")))
                    .Cast<object>()
                    .Select(Convert.ToInt32)
                    .ToList();
                statuses.Should().Equal((int)TestStatus.Active, (int)TestStatus.Active);

                var kind =
                    ((System.Collections.IEnumerable)(await InvokeQueryAsync(
                            generatedAssembly,
                            "KindQuery",
                            fakeOptions)
                        ?? throw new InvalidOperationException("KindQuery returned null.")))
                    .Cast<object>()
                    .Select(Convert.ToInt32)
                    .ToList();
                kind.Should().Equal((int)TestStatus.Active);

                var optionalKind =
                    ((System.Collections.IEnumerable)(await InvokeQueryAsync(
                            generatedAssembly,
                            "OptionalKindQuery",
                            fakeOptions)
                        ?? throw new InvalidOperationException("OptionalKindQuery returned null.")))
                    .Cast<object?>()
                    .Select(value => value is null ? (int?)null : Convert.ToInt32(value))
                    .ToList();
                optionalKind.Should().Equal((int)TestStatus.Active);
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Precompiled_generation_fails_when_materialization_reads_collection_backing_fields()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;
                              using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              });
                              }
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              public List<string> Tags { get; set; } = [];
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<TestItem>> ExecuteEntities(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .ToListAsync();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            IReadOnlyList<ScaffoldedFile> generatedFiles;
            try
            {
                generatedFiles =
                [
                    .. new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                        compilation,
                        SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                        context,
                        new Dictionary<MemberInfo, QualifiedName>(),
                        errors,
                        new HashSet<string>(),
                        assembly)
                ];
            }
            catch (NotSupportedException exception)
            {
                exception.Message.Should().Contain("backing field");
                exception.Message.Should().Contain("NativeAOT");
                return;
            }

            // EF's generator surfaces provider failures as precompilation errors instead of
            // exceptions in some paths; both forms must name the field-read problem.
            errors.Should().NotBeEmpty();
            errors
                .Select(error => error.Exception.Message)
                .Should()
                .Contain(message => message.Contains("backing field"));
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Default property-access mode on purpose: complex-collection initialization only writes the
    // collection field, so it must precompile without UsePropertyAccessMode(PreferProperty).
    // The complex property shares the empty-ValueBuffer rewrite with complex-collection elements.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Generated_interceptor_materializes_complex_collection_and_complex_property()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                                  {
                                      modelBuilder.Entity<TestItem>(entity =>
                                      {
                                          entity.HasPartitionKey(item => item.Pk);
                                          entity.ComplexProperty(item => item.Details);
                                          entity.ComplexCollection(item => item.Answers);
                                      });
                                  }
                              }

                              public sealed class TestItem
                              {
                                  public string Pk { get; set; } = null!;
                                  public string Name { get; set; } = null!;
                                  public TestDetails Details { get; set; } = new();
                                  public List<TestAnswer> Answers { get; set; } = [];
                              }

                              public sealed class TestDetails
                              {
                                  public string Summary { get; set; } = null!;
                                  public int Level { get; set; }
                              }

                              public sealed class TestAnswer
                              {
                                  public string Text { get; set; } = null!;
                                  public bool IsCorrect { get; set; }
                              }

                              public static class QueryContainer
                              {
                                  public static async Task<List<string>> ExecuteEntitySummaries(
                                      DbContextOptions options)
                                  {
                                      await using var context = new TestContext(options);
                                      var items = await context.Items
                                          .Where(item => item.Pk == "tenant-1")
                                          .ToListAsync();

                                      return items
                                          .Select(item => item.Pk
                                              + "|" + item.Details.Summary
                                              + "|" + item.Details.Level
                                              + "|" + string.Join(
                                                  ",",
                                                  item.Answers.Select(answer
                                                      => answer.Text + ":" + answer.IsCorrect)))
                                          .ToList();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
            // Every MaterializationContext, including complex-type element materializers, must
            // pass an addressable empty ValueBuffer: `in default(ValueBuffer)` does not compile.
            generatedCode.Should().Contain("dynamoEmptyValueBuffer");
            generatedCode.Should().NotContain("in default(ValueBuffer)");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty();

            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    ["tenant-1"] = new()
                    {
                        ["pk"] = new() { S = "tenant-1" },
                        ["name"] = new() { S = "name-1" },
                        ["$type"] = new() { S = "TestItem" },
                        ["details"] = new()
                        {
                            M = new()
                            {
                                ["summary"] = new() { S = "first" },
                                ["level"] = new() { N = "3" }
                            }
                        },
                        ["answers"] = new()
                        {
                            L =
                            [
                                new()
                                {
                                    M = new()
                                    {
                                        ["text"] = new() { S = "yes" },
                                        ["isCorrect"] = new() { BOOL = true }
                                    }
                                },
                                new()
                                {
                                    M = new()
                                    {
                                        ["text"] = new() { S = "no" },
                                        ["isCorrect"] = new() { BOOL = false }
                                    }
                                }
                            ]
                        }
                    }
                };
                var fakeOptions = new DbContextOptionsBuilder()
                    // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                    .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .UseDynamo(configure
                        => configure.DynamoDbClient(
                            CompiledModelExecutionTests.CreateFakeClient(store)))
                    .Options;

                var summaries =
                    (List<string>)(await InvokeQueryAsync(
                            generatedAssembly,
                            "ExecuteEntitySummaries",
                            fakeOptions)
                        ?? throw new InvalidOperationException(
                            "ExecuteEntitySummaries returned null."));
                summaries.Should().Equal("tenant-1|first|3|yes:True,no:False");
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Regression coverage for #335: EF Core's precompiled-query generator collects
    // [UnsafeAccessor] declarations on a single translator instance shared across every source
    // file in the compilation, and copies the whole accumulated set into each generated file
    // rather than resetting it per file (upstream EF Core defect - private, non-virtual state).
    // A file whose own queries touch only one entity type can therefore end up with accessors for
    // entity types other files' queries touch, in namespaces this file never imports. Reproduces
    // with three entities across three namespaces and three repositories, each in its own file,
    // precompiled together - the shape a consolidated (single-project, multi-repository)
    // application naturally has.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Generated_interceptors_fully_qualify_accessor_types_across_namespaces()
    {
        const string dataSource = """
                                  using Microsoft.EntityFrameworkCore;

                                  namespace GeneratedQueryTest.NamespaceA { public sealed class ItemA { public string Pk { get; set; } = null!; } }
                                  namespace GeneratedQueryTest.NamespaceB { public sealed class ItemB { public string Pk { get; set; } = null!; } }
                                  namespace GeneratedQueryTest.NamespaceC { public sealed class ItemC { public string Pk { get; set; } = null!; } }

                                  namespace GeneratedQueryTest
                                  {
                                      using NamespaceA;
                                      using NamespaceB;
                                      using NamespaceC;

                                      public sealed class TestContext(DbContextOptions options) : DbContext(options)
                                      {
                                          public DbSet<ItemA> ItemsA => Set<ItemA>();
                                          public DbSet<ItemB> ItemsB => Set<ItemB>();
                                          public DbSet<ItemC> ItemsC => Set<ItemC>();

                                          protected override void OnModelCreating(ModelBuilder modelBuilder)
                                          {
                                              modelBuilder.Entity<ItemA>(entity => entity.HasPartitionKey(item => item.Pk));
                                              modelBuilder.Entity<ItemB>(entity => entity.HasPartitionKey(item => item.Pk));
                                              modelBuilder.Entity<ItemC>(entity => entity.HasPartitionKey(item => item.Pk));
                                          }
                                      }
                                  }
                                  """;
        const string repoASource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading;
                                   using System.Threading.Tasks;
                                   using GeneratedQueryTest;
                                   using GeneratedQueryTest.NamespaceA;
                                   using Microsoft.EntityFrameworkCore;

                                   namespace RepositoryA;

                                   public static class RepoA
                                   {
                                       public static async Task<List<ItemA>> Get(DbContextOptions options, string key, CancellationToken cancellationToken)
                                       {
                                           await using var context = new TestContext(options);
                                           var pk = key;
                                           var token = cancellationToken;
                                           var task = context.ItemsA.Where(item => item.Pk == pk).ToListAsync(token);
                                           return await task.ConfigureAwait(false);
                                       }
                                   }
                                   """;
        const string repoBSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading;
                                   using System.Threading.Tasks;
                                   using GeneratedQueryTest;
                                   using GeneratedQueryTest.NamespaceB;
                                   using Microsoft.EntityFrameworkCore;

                                   namespace RepositoryB;

                                   public static class RepoB
                                   {
                                       public static async Task<List<ItemB>> Get(DbContextOptions options, string key, CancellationToken cancellationToken)
                                       {
                                           await using var context = new TestContext(options);
                                           var pk = key;
                                           var token = cancellationToken;
                                           var task = context.ItemsB.Where(item => item.Pk == pk).ToListAsync(token);
                                           return await task.ConfigureAwait(false);
                                       }
                                   }
                                   """;
        const string repoCSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading;
                                   using System.Threading.Tasks;
                                   using GeneratedQueryTest;
                                   using GeneratedQueryTest.NamespaceC;
                                   using Microsoft.EntityFrameworkCore;

                                   namespace RepositoryC;

                                   public static class RepoC
                                   {
                                       public static async Task<List<ItemC>> Get(DbContextOptions options, string key, CancellationToken cancellationToken)
                                       {
                                           await using var context = new TestContext(options);
                                           var pk = key;
                                           var token = cancellationToken;
                                           var task = context.ItemsC.Where(item => item.Pk == pk).ToListAsync(token);
                                           return await task.ConfigureAwait(false);
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
            "DynamoGeneratedQueryTest",
            [
                CSharpSyntaxTree.ParseText(dataSource, parseOptions, path: "Data.cs"),
                CSharpSyntaxTree.ParseText(repoASource, parseOptions, path: "RepoA.cs"),
                CSharpSyntaxTree.ParseText(repoBSource, parseOptions, path: "RepoB.cs"),
                CSharpSyntaxTree.ParseText(repoCSource, parseOptions, path: "RepoC.cs"),
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            // Three repository files, each precompiling one query: proves this is a genuine
            // multi-file scenario, not a single generated file with multiple query regions.
            generatedFiles.Should().HaveCount(3);

            var repoBFile = generatedFiles.Single(file => file.Path.Contains("RepoB", StringComparison.Ordinal));
            var repoCFile = generatedFiles.Single(file => file.Path.Contains("RepoC", StringComparison.Ordinal));

            // RepoB's own query only touches ItemB, but the shared translator state leaks ItemA's
            // accessor into this file too (see the class remarks on RewriteUnsafeAccessorTypes) -
            // both must be resolvable without relying on `using` directives this file doesn't have.
            repoBFile.Code.Should().Contain("global::GeneratedQueryTest.NamespaceA.ItemA");
            repoBFile.Code.Should().Contain("global::GeneratedQueryTest.NamespaceB.ItemB");
            repoCFile.Code.Should().Contain("global::GeneratedQueryTest.NamespaceA.ItemA");
            repoCFile.Code.Should().Contain("global::GeneratedQueryTest.NamespaceB.ItemB");
            repoCFile.Code.Should().Contain("global::GeneratedQueryTest.NamespaceC.ItemC");
            // No bare, unqualified reference to a foreign-namespace type remains in an unsafe
            // accessor declaration (the shape that produced CS0246 before this fix).
            repoBFile.Code.Should().NotContain("Set(ItemA instance)");
            repoCFile.Code.Should().NotContain("Set(ItemA instance)");
            repoCFile.Code.Should().NotContain("Set(ItemB instance)");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty();

            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    ["a-key"] = new()
                    {
                        ["pk"] = new() { S = "a-key" }, ["$type"] = new() { S = "ItemA" }
                    },
                    ["b-key"] = new()
                    {
                        ["pk"] = new() { S = "b-key" }, ["$type"] = new() { S = "ItemB" }
                    },
                    ["c-key"] = new()
                    {
                        ["pk"] = new() { S = "c-key" }, ["$type"] = new() { S = "ItemC" }
                    }
                };
                var fakeOptions = new DbContextOptionsBuilder()
                    // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                    .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .UseDynamo(configure
                        => configure.DynamoDbClient(
                            CompiledModelExecutionTests.CreateFakeClient(store)))
                    .Options;

                async Task<System.Collections.IEnumerable> InvokeRepoAsync(string typeName, string key)
                {
                    var method = generatedAssembly.GetType(typeName)!.GetMethod("Get")!;
                    var task = (Task)method.Invoke(null, [fakeOptions, key, CancellationToken.None])!;
                    await task;
                    return (System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
                }

                var resultA = await InvokeRepoAsync("RepositoryA.RepoA", "a-key");
                var resultB = await InvokeRepoAsync("RepositoryB.RepoB", "b-key");
                var resultC = await InvokeRepoAsync("RepositoryC.RepoC", "c-key");

                resultA.Cast<object>().Single().GetType().FullName
                    .Should().Be("GeneratedQueryTest.NamespaceA.ItemA");
                resultB.Cast<object>().Single().GetType().FullName
                    .Should().Be("GeneratedQueryTest.NamespaceB.ItemB");
                resultC.Cast<object>().Single().GetType().FullName
                    .Should().Be("GeneratedQueryTest.NamespaceC.ItemC");
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Regression coverage for the namespace-collision case #335 also had to handle: two mapped
    // entity types sharing a simple CLR name but declared in different namespaces. Adding a
    // `using` for the second namespace would still be ambiguous; only a fully qualified reference
    // disambiguates which "Item" an accessor is for.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Generated_interceptors_disambiguate_entity_types_sharing_a_simple_name()
    {
        const string dataSource = """
                                  using Microsoft.EntityFrameworkCore;

                                  namespace GeneratedQueryTest.CollisionA { public sealed class Item { public string Pk { get; set; } = null!; } }
                                  namespace GeneratedQueryTest.CollisionB { public sealed class Item { public string Pk { get; set; } = null!; } }

                                  namespace GeneratedQueryTest
                                  {
                                      public sealed class CollisionContext(DbContextOptions options) : DbContext(options)
                                      {
                                          public DbSet<CollisionA.Item> ItemsA => Set<CollisionA.Item>();
                                          public DbSet<CollisionB.Item> ItemsB => Set<CollisionB.Item>();

                                          protected override void OnModelCreating(ModelBuilder modelBuilder)
                                          {
                                              modelBuilder.Entity<CollisionA.Item>(entity =>
                                              {
                                                  DynamoEntityTypeBuilderExtensions.ToTable(entity, "ItemsA");
                                                  entity.HasPartitionKey(item => item.Pk);
                                              });
                                              modelBuilder.Entity<CollisionB.Item>(entity =>
                                              {
                                                  DynamoEntityTypeBuilderExtensions.ToTable(entity, "ItemsB");
                                                  entity.HasPartitionKey(item => item.Pk);
                                              });
                                          }
                                      }
                                  }
                                  """;
        const string repoASource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading;
                                   using System.Threading.Tasks;
                                   using GeneratedQueryTest;
                                   using GeneratedQueryTest.CollisionA;
                                   using Microsoft.EntityFrameworkCore;

                                   namespace CollisionRepositoryA;

                                   public static class CollisionRepoA
                                   {
                                       public static async Task<List<Item>> Get(DbContextOptions options, string key, CancellationToken cancellationToken)
                                       {
                                           await using var context = new CollisionContext(options);
                                           var pk = key;
                                           var token = cancellationToken;
                                           var task = context.ItemsA.Where(item => item.Pk == pk).ToListAsync(token);
                                           return await task.ConfigureAwait(false);
                                       }
                                   }
                                   """;
        const string repoBSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading;
                                   using System.Threading.Tasks;
                                   using GeneratedQueryTest;
                                   using GeneratedQueryTest.CollisionB;
                                   using Microsoft.EntityFrameworkCore;

                                   namespace CollisionRepositoryB;

                                   public static class CollisionRepoB
                                   {
                                       public static async Task<List<Item>> Get(DbContextOptions options, string key, CancellationToken cancellationToken)
                                       {
                                           await using var context = new CollisionContext(options);
                                           var pk = key;
                                           var token = cancellationToken;
                                           var task = context.ItemsB.Where(item => item.Pk == pk).ToListAsync(token);
                                           return await task.ConfigureAwait(false);
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
            "DynamoGeneratedQueryCollisionTest",
            [
                CSharpSyntaxTree.ParseText(dataSource, parseOptions, path: "CollisionData.cs"),
                CSharpSyntaxTree.ParseText(repoASource, parseOptions, path: "CollisionRepoA.cs"),
                CSharpSyntaxTree.ParseText(repoBSource, parseOptions, path: "CollisionRepoB.cs"),
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.CollisionContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().HaveCount(2);

            var repoBFile = generatedFiles.Single(file => file.Path.Contains("CollisionRepoB", StringComparison.Ordinal));
            // A bare `using GeneratedQueryTest.CollisionA;` would have made "Item" ambiguous here
            // (CollisionA.Item and this file's own CollisionB.Item both resolve); only a fully
            // qualified reference is unambiguous.
            repoBFile.Code.Should().Contain("global::GeneratedQueryTest.CollisionA.Item");
            repoBFile.Code.Should().Contain("global::GeneratedQueryTest.CollisionB.Item");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Regression coverage for an EF Core 10-only precompiler defect discovered while validating
    // the complex-collection fixes above (not caused by this provider): a precompiled no-tracking
    // query generates a local variable named after the entity type's full CLR name, which is
    // invalid C# once that name contains a dot (i.e. the entity type is declared in any
    // namespace — the ordinary case). This is unrelated to complex collections; it reproduces for
    // any no-tracking query. EF Core 11 does not exhibit it. See docs/limitations.md.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Precompiled_no_tracking_query_over_namespaced_entity_reflects_ef_core_10_defect()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                                      => modelBuilder.Entity<TestItem>(entity
                                          => entity.HasPartitionKey(item => item.Pk));
                              }

                              public sealed class TestItem
                              {
                                  public string Pk { get; set; } = null!;
                              }

                              public static class QueryContainer
                              {
                                  public static async Task<List<TestItem>> ExecuteNoTracking(
                                      DbContextOptions options)
                                  {
                                      await using var context = new TestContext(options);
                                      return await context.Items
                                          .AsNoTracking()
                                          .Where(item => item.Pk == "tenant-1")
                                          .ToListAsync();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            var compileErrors = generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();

#if NET10_0
            // Locks in the known EF Core 10 defect so this test starts failing (prompting removal
            // of the workaround and this comment) the day EF Core fixes it upstream.
            compileErrors.Should().NotBeEmpty();
            compileErrors.Select(diagnostic => diagnostic.Id).Should().Contain("CS1003");
#else
            AssertCompilationSucceeded(generatedCompilation);
#endif
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // A complex collection on the same entity must not mask a genuine unsupported field read:
    // the primitive-collection backing-field check stays in force for every other member.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Precompiled_generation_still_rejects_primitive_collection_field_reads_next_to_complex_collections()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                                  {
                                      modelBuilder.Entity<TestItem>(entity =>
                                      {
                                          entity.HasPartitionKey(item => item.Pk);
                                          entity.ComplexCollection(item => item.Answers);
                                      });
                                  }
                              }

                              public sealed class TestItem
                              {
                                  public string Pk { get; set; } = null!;
                                  public List<string> Tags { get; set; } = [];
                                  public List<TestAnswer> Answers { get; set; } = [];
                              }

                              public sealed class TestAnswer
                              {
                                  public string Text { get; set; } = null!;
                              }

                              public static class QueryContainer
                              {
                                  public static async Task<List<TestItem>> ExecuteEntities(
                                      DbContextOptions options)
                                  {
                                      await using var context = new TestContext(options);
                                      return await context.Items
                                          .Where(item => item.Pk == "tenant-1")
                                          .ToListAsync();
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
            "DynamoGeneratedQueryTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "GeneratedQueryTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            string message;
            try
            {
                _ = new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

                errors.Should().NotBeEmpty();
                message = string.Join(
                    Environment.NewLine,
                    errors.Select(error => error.Exception.Message));
            }
            catch (NotSupportedException exception)
            {
                message = exception.Message;
            }

            message.Should().Contain("backing field");
            message.Should().Contain("TestItem.<Tags>k__BackingField");
            message.Should().NotContain("Answers");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Regression coverage for the C# 14 extension-block precompiler defect: EF Core's upstream
    // precompiler could not resolve any method declared inside DynamoDbQueryableExtensions'
    // former `extension<TEntity>(...)` block (reported as "Couldn't find nested type '`1' on
    // containing type 'DynamoDbQueryableExtensions'"). The extension surface is now declared as
    // conventional `this`-parameter static extension methods, which resolve correctly. These
    // tests fail again if the file is ever converted back to an extension block.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Generated_interceptor_precompiles_constant_limit_and_applies_it_to_the_request()
    {
        // This covers only the constant-limit shape. Parameterized `.Limit(limit)` is covered by
        // Generated_interceptor_precompiles_limit_and_next_token_composition_and_applies_both_to_the_request
        // below, alongside parameterized WithNextToken.
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              });
                              }
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<string>> ExecuteWithConstantLimit(
                              DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Limit(5)
                              .Select(item => item.Name)
                              .ToListAsync();
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
            "DynamoLimitPrecompilationTest",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "LimitPrecompilationTest.cs")],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            // This is the exact assertion that reproduced the original defect: before the
            // extension-block-to-classic-method conversion, `errors` contained an
            // InvalidOperationException ("Couldn't find nested type...") and `generatedFiles`
            // was empty.
            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
            generatedCode.Should().Contain("CreateQueryTemplate");
            generatedCode.Should().NotContain("SelectExpressionJson");
            generatedCode.Should().NotContain("RelationalMaterializerLiftableConstantContext");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            // Interceptor binding failures surface as warnings, not errors: an unbound
            // interceptor silently falls back to JIT translation and the assertions below would
            // then pass without the Limit ever having been precompiled.
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty(
                    string.Join(
                        Environment.NewLine,
                        generatedCompilation
                            .GetDiagnostics()
                            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                            .Select(diagnostic => diagnostic.ToString())));

            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    ["tenant-1"] = new()
                    {
                        ["pk"] = new() { S = "tenant-1" }, ["name"] = new() { S = "name-1" }
                    }
                };

                ExecuteStatementRequest? capturedRequest = null;
                var client = Substitute.For<IAmazonDynamoDB>();
                client
                    .ExecuteStatementAsync(
                        Arg.Do<ExecuteStatementRequest>(request => capturedRequest = request),
                        Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<ExecuteStatementRequest>();
                        return Task.FromResult(
                            new ExecuteStatementResponse
                            {
                                Items = store.Values.Take(request.Limit ?? int.MaxValue).ToList()
                            });
                    });
                var fakeOptions = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(configure
                        => configure.DynamoDbClient(client))
                    .Options;

                await InvokeQueryAsync(generatedAssembly, "ExecuteWithConstantLimit", fakeOptions);
                capturedRequest.Should().NotBeNull();
                // Proves the DynamoDB request actually carries the intended Limit value, not
                // merely that the LINQ result count happened to be small.
                capturedRequest!.Limit.Should().Be(5);
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Covers the fluent surface not already exercised by the Limit/WithNextToken tests above and
    // below: a parameterless marker (WithConsistentRead), a value-carrying extension (WithIndex),
    // and another independent parameterless marker (AllowScan). WithNextToken itself is covered by
    // Generated_interceptor_precompiles_limit_and_next_token_composition_and_applies_both_to_the_request.
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Generated_interceptor_precompiles_the_remaining_fluent_extension_surface()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              entity.HasGlobalSecondaryIndex("ByName", nameof(TestItem.Name));
                              });
                              }
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              }

                              public static class QueryContainer
                              {
                              // Parameterless fluent marker.
                              public static async Task<List<string>> ExecuteWithConsistentRead(
                              DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .WithConsistentRead(true)
                              .Select(item => item.Name)
                              .ToListAsync();
                              }

                              // Fluent extension carrying a value argument.
                              public static async Task<List<string>> ExecuteWithIndex(
                              DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Name == "tenant-1")
                              .WithIndex("ByName")
                              .Select(item => item.Pk)
                              .ToListAsync();
                              }

                              // Another parameterless fluent marker with an independent method
                              // body (not a delegating overload).
                              public static async Task<List<string>> ExecuteAllowScan(
                              DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              return await context.Items
                              .Where(item => item.Name == "tenant-1")
                              .AllowScan()
                              .Select(item => item.Pk)
                              .ToListAsync();
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
            "DynamoFluentSurfacePrecompilationTest",
            [
                CSharpSyntaxTree.ParseText(
                    source,
                    parseOptions,
                    path: "FluentSurfacePrecompilationTest.cs")
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
            // Three query roots (WithConsistentRead, WithIndex, AllowScan) must each produce
            // a generated executor; a regression that reintroduces the extension-block shape
            // drops this count to zero.
            Regex
                .Matches(generatedCode, "CreateQueryTemplate")
                .Count
                .Should()
                .BeGreaterThanOrEqualTo(3);
            generatedCode.Should().NotContain("SelectExpressionJson");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty(
                    string.Join(
                        Environment.NewLine,
                        generatedCompilation
                            .GetDiagnostics()
                            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                            .Select(diagnostic => diagnostic.ToString())));
        }
        finally
        {
            loadContext.Unload();
        }
    }

    // Proves the realistic pagination composition precompiles and that the generated interceptor
    // genuinely parameterizes both the limit and the continuation token — the same generated
    // executor is invoked twice with different runtime values, and the DynamoDB requests it sends
    // are captured directly (not inferred from result counts).
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Generated_interceptor_precompiles_limit_and_next_token_composition_and_applies_both_to_the_request()
    {
        const string source = """
                              using System.Collections.Generic;
                                   using System.Linq;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              {
                              modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              });
                              }
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              }

                              public static class QueryContainer
                              {
                              public static async Task<List<string>> LoadNextPage(
                              DbContextOptions options,
                              int pageSizeArg,
                              string nextTokenArg)
                              {
                              await using var context = new TestContext(options);
                              var pageSize = pageSizeArg;
                              var nextToken = nextTokenArg;
                              return await context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Limit(pageSize)
                              .WithNextToken(nextToken)
                              .Select(item => item.Name)
                              .ToListAsync();
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
            "DynamoPaginationCompositionTest",
            [
                CSharpSyntaxTree.ParseText(
                    source,
                    parseOptions,
                    path: "PaginationCompositionTest.cs")
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertCompilationSucceeded(compilation);
        var (loadContext, assembly) = EmitAndLoad(compilation);

        try
        {
            var options = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options;
            await using var context = (DbContext)Activator.CreateInstance(
                assembly.GetType("GeneratedQueryTest.TestContext")!,
                options)!;
            using var workspace = new AdhocWorkspace();
            var errors = new List<PrecompiledQueryCodeGenerator.QueryPrecompilationError>();
            var generatedFiles =
                new DynamoPrecompiledQueryCodeGenerator().GeneratePrecompiledQueries(
                    compilation,
                    SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp),
                    context,
                    new Dictionary<MemberInfo, QualifiedName>(),
                    errors,
                    new HashSet<string>(),
                    assembly);

            errors.Should().BeEmpty();
            generatedFiles.Should().NotBeEmpty();
            var generatedCode =
                string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
            generatedCode.Should().Contain("CreateQueryTemplate");
            generatedCode.Should().NotContain("SelectExpressionJson");
            generatedCode.Should().NotContain("RelationalMaterializerLiftableConstantContext");
            // The generated Limit/WithNextToken wrapper methods must carry the runtime `limit` and
            // `nextToken` values as real query parameters (queryContext.Parameters.Add(...)), not
            // embed a specific value as a PartiQL literal. Generated parameter names are positional
            // ("p", "p2", ...), not the original source identifiers, so assert on the mechanism
            // rather than a specific name.
            generatedCode.Should().Contain("queryContext.Parameters.Add(\"p\", limit)");
            generatedCode.Should().Contain("DynamoGeneratedQueryRuntime.ValidateWithNextToken");
            generatedCode.Should().NotContain("\"tenant-1-token-a\"");
            generatedCode.Should().NotContain("\"tenant-1-token-b\"");

            var generatedCompilation = compilation.AddSyntaxTrees(
                generatedFiles.Select(file
                    => CSharpSyntaxTree.ParseText(file.Code, parseOptions, file.Path)));
            AssertCompilationSucceeded(generatedCompilation);
            generatedCompilation
                .GetDiagnostics()
                .Where(diagnostic
                    => diagnostic.Severity >= DiagnosticSeverity.Warning
                    && InterceptorWarningIds.Contains(diagnostic.Id))
                .Should()
                .BeEmpty(
                    string.Join(
                        Environment.NewLine,
                        generatedCompilation
                            .GetDiagnostics()
                            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                            .Select(diagnostic => diagnostic.ToString())));

            var (generatedLoadContext, generatedAssembly) = EmitAndLoad(generatedCompilation);
            try
            {
                var store = new Dictionary<string, Dictionary<string, AttributeValue>>
                {
                    ["tenant-1"] = new()
                    {
                        ["pk"] = new() { S = "tenant-1" }, ["name"] = new() { S = "name-1" }
                    }
                };

                var capturedRequests = new List<ExecuteStatementRequest>();
                var client = Substitute.For<IAmazonDynamoDB>();
                client
                    .ExecuteStatementAsync(
                        Arg.Do<ExecuteStatementRequest>(capturedRequests.Add),
                        Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var request = callInfo.Arg<ExecuteStatementRequest>();
                        return Task.FromResult(
                            new ExecuteStatementResponse
                            {
                                Items = store.Values.Take(request.Limit ?? int.MaxValue).ToList()
                            });
                    });
                var fakeOptions = new DbContextOptionsBuilder()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo(configure
                        => configure.DynamoDbClient(client))
                    .Options;

                // First invocation: pageSize=5, a real (bootstrapped-in-a-real-scenario) token.
                await InvokeQueryAsync(
                    generatedAssembly,
                    "LoadNextPage",
                    fakeOptions,
                    [5, "tenant-1-token-a"]);
                capturedRequests.Should().HaveCount(1);
                capturedRequests[0].Limit.Should().Be(5);
                capturedRequests[0].NextToken.Should().Be("tenant-1-token-a");

                // Second invocation of the SAME generated interceptor with different runtime
                // values: proves the interceptor is reused/parameterized rather than regenerated
                // or having baked in the first call's values.
                await InvokeQueryAsync(
                    generatedAssembly,
                    "LoadNextPage",
                    fakeOptions,
                    [2, "tenant-1-token-b"]);
                capturedRequests.Should().HaveCount(2);
                capturedRequests[1].Limit.Should().Be(2);
                capturedRequests[1].NextToken.Should().Be("tenant-1-token-b");
            }
            finally
            {
                generatedLoadContext.Unload();
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

    private static readonly HashSet<string> InterceptorWarningIds =
    [
        "CS9192", "CS9193", "CS9200", "CS9201", "CS9202", "CS9225"
    ];

    private static async Task<object?> InvokeQueryAsync(
        Assembly assembly,
        string methodName,
        DbContextOptions options)
        => await InvokeQueryAsync(assembly, methodName, options, []);

    private static async Task<object?> InvokeQueryAsync(
        Assembly assembly,
        string methodName,
        DbContextOptions options,
        object?[] additionalArguments)
    {
        var method = assembly.GetType("GeneratedQueryTest.QueryContainer")!.GetMethod(methodName)!;
        var invoke = method.Invoke(null, [options, .. additionalArguments]);
        if (invoke is Task task)
        {
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task);
        }

        return invoke;
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
            nameof(PrecompiledQueryGenerationTests),
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

#pragma warning disable EF9100
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Compiled_model_primes_collection_codecs_for_native_aot()
    {
        using var context = new CollectionContext(
            new DbContextOptionsBuilder<CollectionContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);

        var designTimeModel = context.GetService<IDesignTimeModel>()!.Model;
        var typeMappingSource = context.GetService<ITypeMappingSource>()!;
        var cSharpHelper = new CSharpHelper(typeMappingSource);
        var generator = new CSharpRuntimeModelCodeGenerator(
            new DynamoCSharpRuntimeAnnotationCodeGenerator(
                new CSharpRuntimeAnnotationCodeGeneratorDependencies(cSharpHelper)),
            cSharpHelper);
        var generatedFiles = generator.GenerateModel(
            designTimeModel,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(CollectionContext),
                ModelNamespace = nameof(CollectionContext),
                ForNativeAot = true
            });

        var generatedCode =
            string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
        generatedCode.Should().Contain("PrimeListMapping<");
        generatedCode.Should().Contain("PrimeSetMapping<HashSet<int>, int>(");
        generatedCode
            .Should()
            .Contain("PrimeDictionaryMapping<Dictionary<string, decimal>, decimal>(");

        // The emitted clone expression returns CoreTypeMapping, so the prime call must cast it.
        generatedCode.Should().Contain("(DynamoTypeMapping)(");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Compiled_model_primes_converted_element_collection_codecs_for_native_aot()
    {
        using var context = new CollectionContext(
            new DbContextOptionsBuilder<CollectionContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);

        var designTimeModel = context.GetService<IDesignTimeModel>()!.Model;
        var typeMappingSource = context.GetService<ITypeMappingSource>()!;
        var cSharpHelper = new CSharpHelper(typeMappingSource);
        var generator = new CSharpRuntimeModelCodeGenerator(
            new DynamoCSharpRuntimeAnnotationCodeGenerator(
                new CSharpRuntimeAnnotationCodeGeneratorDependencies(cSharpHelper)),
            cSharpHelper);
        var generatedFiles = generator.GenerateModel(
            designTimeModel,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(CollectionContext),
                ModelNamespace = nameof(CollectionContext),
                ForNativeAot = true
            });

        var generatedCode =
            string.Join(Environment.NewLine, generatedFiles.Select(file => file.Code));
        generatedCode.Should().Contain("PrimeListMapping<List<Guid>, Guid>(");
        generatedCode
            .Should()
            .Contain(
                "PrimeListMapping<List<PrecompiledQueryGenerationTests.ConvertedStatus>, "
                + "PrecompiledQueryGenerationTests.ConvertedStatus>(");
        generatedCode.Should().Contain("PrimeSetMapping<HashSet<Guid>, Guid>(");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Compiled_model_generation_fails_fast_for_converted_primitive_collections()
    {
        using var context = new ConvertedCollectionContext(
            new DbContextOptionsBuilder<ConvertedCollectionContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);
        var designTimeModel = context.GetService<IDesignTimeModel>()!.Model;
        var typeMappingSource = context.GetService<ITypeMappingSource>()!;
        var cSharpHelper = new CSharpHelper(typeMappingSource);
        var generator = new CSharpRuntimeModelCodeGenerator(
            new DynamoCSharpRuntimeAnnotationCodeGenerator(
                new CSharpRuntimeAnnotationCodeGeneratorDependencies(cSharpHelper)),
            cSharpHelper);

        var generate = () => generator.GenerateModel(
            designTimeModel,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(ConvertedCollectionContext),
                ModelNamespace = nameof(ConvertedCollectionContext),
                ForNativeAot = true
            });

        generate
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*property-level value converter*not supported*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Primed_collection_mappings_round_trip_through_boxed_boundary()
    {
        using var context = new CollectionContext(
            new DbContextOptionsBuilder<CollectionContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);
        var entityType = context.Model.FindEntityType(typeof(CollectionItem))!;

        var flagsMapping = (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.Flags))!
            .GetTypeMapping();
        var primedFlagsMapping =
            DynamoGeneratedModelRuntime.PrimeSetMapping<HashSet<int>, int>(flagsMapping);
        primedFlagsMapping
            .CreateAttributeValue(new HashSet<int> { 7, 11 }, typeof(HashSet<int>))
            .NS
            .Should()
            .BeEquivalentTo("7", "11");

        var chargesMapping =
            (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.Charges))!
                .GetTypeMapping();
        var primedChargesMapping = DynamoGeneratedModelRuntime
            .PrimeDictionaryMapping<Dictionary<string, decimal>, decimal>(chargesMapping);
        primedChargesMapping
            .CreateAttributeValue(
                new Dictionary<string, decimal> { ["tax"] = 1.25m },
                typeof(Dictionary<string, decimal>))
            .M["tax"]
            .N
            .Should()
            .Be("1.25");

        var optionalScoresMapping =
            (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.OptionalScores))!
                .GetTypeMapping();
        var primedOptionalScoresMapping =
            DynamoGeneratedModelRuntime.PrimeListMapping<List<int?>, int?>(optionalScoresMapping);
        var optionalScores =
            primedOptionalScoresMapping.CreateAttributeValue(
                new List<int?> { null, 42 },
                typeof(List<int?>));
        optionalScores.L.Should().HaveCount(2);
        optionalScores.L[0].NULL.Should().BeTrue();
        optionalScores.L[1].N.Should().Be("42");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Primed_converted_element_collection_mappings_round_trip_through_boxed_boundary()
    {
        using var context = new CollectionContext(
            new DbContextOptionsBuilder<CollectionContext>()
                // EF's service-provider counter is process-wide; distinct configurations intentionally create distinct providers.
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseDynamo().Options);
        var entityType = context.Model.FindEntityType(typeof(CollectionItem))!;

        var convertedIdsMapping =
            (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.ConvertedIds))!
                .GetTypeMapping();
        var primedConvertedIdsMapping =
            DynamoGeneratedModelRuntime.PrimeListMapping<List<Guid>, Guid>(convertedIdsMapping);
        var expectedId = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        var convertedIds = primedConvertedIdsMapping.CreateAttributeValue(
            new List<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") },
            typeof(List<Guid>));
        convertedIds.L.Should().HaveCount(1);
        convertedIds.L[0].S.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        ReadBoxed(primedConvertedIdsMapping, convertedIds)
            .Should()
            .BeEquivalentTo(new List<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") });

        var statusesMapping =
            (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.Statuses))!
                .GetTypeMapping();
        var primedStatusesMapping =
            DynamoGeneratedModelRuntime.PrimeListMapping<List<ConvertedStatus>, ConvertedStatus>(
                statusesMapping);
        var statuses = primedStatusesMapping.CreateAttributeValue(
            new List<ConvertedStatus> { ConvertedStatus.Active },
            typeof(List<ConvertedStatus>));
        statuses.L.Should().HaveCount(1);
        statuses.L[0].S.Should().Be("Active");
        ReadBoxed(primedStatusesMapping, statuses)
            .Should()
            .BeEquivalentTo(new List<ConvertedStatus> { ConvertedStatus.Active });

        var convertedFlagsMapping =
            (DynamoTypeMapping)entityType.FindProperty(nameof(CollectionItem.ConvertedFlags))!
                .GetTypeMapping();
        var primedConvertedFlagsMapping =
            DynamoGeneratedModelRuntime.PrimeSetMapping<HashSet<Guid>, Guid>(convertedFlagsMapping);
        var convertedFlags = primedConvertedFlagsMapping.CreateAttributeValue(
            new HashSet<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") },
            typeof(HashSet<Guid>));
        convertedFlags.SS.Should().Equal("0f8fad5b-d9cb-469f-a165-70867728950e");
        ReadBoxed(primedConvertedFlagsMapping, convertedFlags)
            .Should()
            .BeEquivalentTo(new HashSet<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") });
    }

    private static object? ReadBoxed(DynamoTypeMapping mapping, AttributeValue attributeValue)
        => mapping.ReaderWriter!.ReadObject(attributeValue, "Path", true, null);

    private sealed class ConvertedCollectionContext(
        DbContextOptions<ConvertedCollectionContext> options) : DbContext(options)
    {
        public DbSet<ConvertedCollectionItem> Items => Set<ConvertedCollectionItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedCollectionItem>(entity =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(entity, "ConvertedCollectionItems");
                entity.HasPartitionKey(item => item.Pk);
                entity
                    .Property(item => item.Scores)
                    .HasConversion(
                        scores => string.Join(',', scores),
                        value => value
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(value => int.Parse(value))
                            .ToList());
            });
    }

    private sealed class ConvertedCollectionItem
    {
        public string Pk { get; set; } = null!;

        public List<int> Scores { get; set; } = [];
    }

    private sealed class CollectionContext(DbContextOptions<CollectionContext> options) : DbContext(
        options)
    {
        public DbSet<CollectionItem> Items => Set<CollectionItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CollectionItem>(entity =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(entity, "CollectionItems");
                entity.HasPartitionKey(item => item.Pk);
                entity
                    .PrimitiveCollection(item => item.ConvertedIds)
                    .ElementType(e => e.HasConversion<string>());
                entity
                    .PrimitiveCollection(item => item.Statuses)
                    .ElementType(e => e.HasConversion<string>());
                entity
                    .PrimitiveCollection(item => item.ConvertedFlags)
                    .ElementType(e => e.HasConversion<string>());
            });
    }

    private sealed class CollectionItem
    {
        public string Pk { get; set; } = null!;

        public List<int> Scores { get; set; } = [];

        public HashSet<int> Flags { get; set; } = [];

        public Dictionary<string, decimal> Charges { get; set; } = [];

        public List<int?> OptionalScores { get; set; } = [];

        public List<Guid> ConvertedIds { get; set; } = [];

        public List<ConvertedStatus> Statuses { get; set; } = [];

        public HashSet<Guid> ConvertedFlags { get; set; } = [];
    }

    private enum ConvertedStatus
    {
        Active
    }

    private static IProperty ResolveProperty(
        IModel model,
        string declaringTypeName,
        string propertyName)
    {
        var method = typeof(DynamoGeneratedQueryRuntime).GetMethod(
            "ResolveProperty",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (IProperty)method.Invoke(null, [model, declaringTypeName, propertyName])!;
        }
        catch (TargetInvocationException exception) when (
            exception.InnerException is InvalidOperationException innerException)
        {
            throw innerException;
        }
    }
#pragma warning restore EF9100

    private static readonly string RelationalExecutorPreamble = string.Join(
        '\n',
        [
            "            var relationalModel = dbContext.Model.GetRelationalModel();",
            "            var relationalTypeMappingSource = dbContext.GetService<IRelationalTypeMappingSource>();",
            "            var materializerLiftableConstantContext = new RelationalMaterializerLiftableConstantContext(",
            "                dbContext.GetService<ShapedQueryCompilingExpressionVisitorDependencies>(),",
            "                dbContext.GetService<RelationalShapedQueryCompilingExpressionVisitorDependencies>(),",
            "                dbContext.GetService<RelationalCommandBuilderDependencies>());",
            ""
        ]);

    private sealed class InheritanceContext(DbContextOptions<InheritanceContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseItem>(entity =>
            {
                entity.HasPartitionKey(item => item.Pk);
                entity.Property(item => item.Status).HasConversion<string>();
            });
            modelBuilder.Entity<DerivedItem>().HasBaseType<BaseItem>();
            modelBuilder.Entity<SiblingItem>().HasBaseType<BaseItem>();
        }
    }

    private class BaseItem
    {
        public string Pk { get; set; } = null!;
        public TestStatus Status { get; set; }
    }

    private sealed class DerivedItem : BaseItem { }

    private sealed class SiblingItem : BaseItem { }

    private sealed class DuplicateNameContext(DbContextOptions<DuplicateNameContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<First.Widget>(entity =>
            {
                entity.HasPartitionKey(item => item.Pk);
                DynamoEntityTypeBuilderExtensions.ToTable(entity, "FirstWidgets");
            });
            modelBuilder.Entity<Second.Widget>(entity =>
            {
                entity.HasPartitionKey(item => item.Pk);
                DynamoEntityTypeBuilderExtensions.ToTable(entity, "SecondWidgets");
            });
        }
    }

    private enum TestStatus
    {
        Active
    }
}
