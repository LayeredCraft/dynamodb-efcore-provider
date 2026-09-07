using System.Reflection;
using System.Runtime.Loader;
using EntityFrameworkCore.DynamoDb.Design.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace EntityFrameworkCore.DynamoDb.AotTests;

public class PrecompiledQueryGenerationTests
{
    [Fact]
    public void Generated_runtime_resolves_an_inherited_property_once()
    {
        using var context = new InheritanceContext(
            new DbContextOptionsBuilder<InheritanceContext>().UseDynamo().Options);
        var property = ResolveProperty(
            context.Model,
            typeof(BaseItem).FullName!,
            nameof(BaseItem.Status));

        property.DeclaringType.Name.Should().Be(typeof(BaseItem).FullName);
        property.Name.Should().Be(nameof(BaseItem.Status));
    }

    [Fact]
    public void Generated_runtime_reports_missing_inherited_property()
    {
        using var context = new InheritanceContext(
            new DbContextOptionsBuilder<InheritanceContext>().UseDynamo().Options);

        var action = () => ResolveProperty(context.Model, typeof(BaseItem).FullName!, "Missing");

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*BaseItem.Missing*was not found*");
    }

    [Fact(Timeout = 60_000)]
    public async Task
        Generated_interceptor_compiles_and_upstream_executor_template_matches_rewrite_contract()
    {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.EntityFrameworkCore;

                              namespace GeneratedQueryTest;

                              public sealed class TestContext(DbContextOptions options) : DbContext(options)
                              {
                                  public DbSet<TestItem> Items => Set<TestItem>();

                                  protected override void OnModelCreating(ModelBuilder modelBuilder)
                              => modelBuilder.Entity<TestItem>(entity =>
                              {
                              entity.HasPartitionKey(item => item.Pk);
                              entity.Property(item => item.Status).HasConversion<string>();
                              });
                              }

                              public sealed class TestItem
                              {
                              public string Pk { get; set; } = null!;
                              public string Name { get; set; } = null!;
                              public TestStatus Status { get; set; }
                              public List<string> Tags { get; set; } = [];
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

                              public static List<string> ExecuteSynchronously(DbContextOptions options)
                              {
                              using var context = new TestContext(options);
                              return context.Items
                              .Where(item => item.Pk == "tenant-1")
                              .Select(item => item.Name)
                              .ToList();
                              }

                              public static async Task<List<TestItem>> ExecuteEntities(DbContextOptions options)
                              {
                              await using var context = new TestContext(options);
                              string[] keys = ["tenant-1", "tenant-2"];
                              return await context.Items
                              .Where(item => keys.Contains(item.Pk))
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
            var options = new DbContextOptionsBuilder().UseDynamo().Options;
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
            generatedCode.Should().Contain("CreateValueReader");
            generatedCode.Should().Contain("InterceptsLocationAttribute(1,");
            generatedCode.Should().NotContain("SelectExpressionJson");
            generatedCode.Should().NotContain("RelationalMaterializerLiftableConstantContext");

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

    private enum TestStatus
    {
        Active
    }
}
