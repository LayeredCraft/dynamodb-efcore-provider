using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
#if NET11_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Query.Inheritance;
#endif

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class ComplianceDynamoTest : ComplianceTestBase
{
    [ConditionalFact]
    public void Spec_tests_do_not_add_skipped_no_op_overrides()
    {
        var sourceRoot = LocateSourceRoot();
        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("ComplianceDynamoTest.cs", StringComparison.Ordinal))
            .Where(path => !AllowedLegacyNoOpOverrideFiles.Contains(
                Path.GetRelativePath(sourceRoot, path).Replace(Path.DirectorySeparatorChar, '/')))
            .SelectMany(path => FindSkippedNoOpOverrides(sourceRoot, path, File.ReadAllText(path)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    protected override Assembly TargetAssembly { get; } = typeof(ComplianceDynamoTest).Assembly;

    private static readonly ISet<string> AllowedLegacyNoOpOverrideFiles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "FindDynamoTest.cs",
            "LoggingDynamoTest.cs",
            "MaterializationInterceptionDynamoTest.cs",
            "OverzealousInitializationDynamoTest.cs",
            "Query/NorthwindAsNoTrackingQueryDynamoTest.cs",
            "Query/NorthwindSelectQueryDynamoTest.cs",
            "QueryExpressionInterceptionDynamoTest.cs",
            "SaveChangesInterceptionDynamoTest.cs"
        };

    private static string LocateSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tests",
                "EntityFrameworkCore.DynamoDb.SpecificationTests");

            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate tests/EntityFrameworkCore.DynamoDb.SpecificationTests.");
    }

    private static IEnumerable<string> FindSkippedNoOpOverrides(
        string sourceRoot,
        string path,
        string source)
    {
        var pattern = new Regex(
            @"\[Conditional(?:Fact|Theory)\([^\]]*Skip\s*=\s*[^\]]+\)\][\s\S]*?public\s+override\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void|\w+)\s+(?<method>\w+)\s*\([^)]*\)[\s\S]*?(?:=>\s*Task\.CompletedTask\s*;|=>\s*default\s*;|\{\s*(?:return\s*;)?\s*\})",
            RegexOptions.Multiline);

        foreach (Match match in pattern.Matches(source))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, path);
            yield return $"{relativePath}: {match.Groups["method"].Value}";
        }
    }

    protected override IEnumerable<Type> GetBaseTestClasses()
    {
        yield return typeof(ApiConsistencyTestBase<>);
        yield return typeof(BuiltInDataTypesTestBase<>);
        yield return typeof(ComplexTypeQueryTestBase<>);
        yield return typeof(ComplexTypesTrackingTestBase<>);
        yield return typeof(CompositeKeyEndToEndTestBase<>);
        yield return typeof(ConvertToProviderTypesTestBase<>);
        yield return typeof(CustomConvertersTestBase<>);
        yield return typeof(ComplianceTestBase);
#if NET11_0_OR_GREATER
        yield return typeof(EntityFrameworkServiceCollectionExtensionsTestBase);
#endif
        yield return typeof(ConcurrencyDetectorDisabledTestBase<>);
        yield return typeof(ConcurrencyDetectorEnabledTestBase<>);
        yield return typeof(FindTestBase<>);
        yield return typeof(FunkyDataQueryTestBase<>);
        yield return typeof(EnumTranslationsTestBase<>);
        yield return typeof(GuidTranslationsTestBase<>);
        yield return typeof(FiltersInheritanceQueryTestBase<>);
        yield return typeof(InheritanceQueryTestBase<>);
#if NET11_0_OR_GREATER
        yield return typeof(InheritanceComplexTypesQueryTestBase<>);
#endif
        yield return typeof(StringTranslationsTestBase<>);
        yield return typeof(KeysWithConvertersTestBase<>);
        yield return typeof(LoggingTestBase);
        yield return typeof(MaterializationInterceptionTestBase<>);
        yield return typeof(OverzealousInitializationTestBase<>);
        yield return typeof(PrimitiveCollectionsQueryTestBase<>);
        yield return typeof(QueryExpressionInterceptionTestBase);
        yield return typeof(SaveChangesInterceptionTestBase);
        yield return typeof(SeedingTestBase);
        yield return typeof(ValueConvertersEndToEndTestBase<>);
        yield return typeof(ComplexPropertiesMiscellaneousTestBase<>);
        yield return typeof(ComplexPropertiesProjectionTestBase<>);
        yield return typeof(ComplexPropertiesStructuralEqualityTestBase<>);
        yield return typeof(ByteArrayTranslationsTestBase<>);
        yield return typeof(ComparisonOperatorTranslationsTestBase<>);
        yield return typeof(DateTimeTranslationsTestBase<>);
        yield return typeof(LogicalOperatorTranslationsTestBase<>);
        yield return typeof(NorthwindAsNoTrackingQueryTestBase<>);
        yield return typeof(NorthwindAsTrackingQueryTestBase<>);
        yield return typeof(NorthwindChangeTrackingQueryTestBase<>);
        yield return typeof(NorthwindFunctionsQueryTestBase<>);
        yield return typeof(NorthwindQueryFiltersQueryTestBase<>);
        yield return typeof(NorthwindQueryTaggingQueryTestBase<>);
        yield return typeof(NorthwindSelectQueryTestBase<>);
        yield return typeof(NorthwindWhereQueryTestBase<>);
    }
}
