using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Microsoft.EntityFrameworkCore.Types;
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
            .SelectMany(path => FindSkippedNoOpOverrides(sourceRoot, path, File.ReadAllText(path)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [ConditionalFact]
    public void Spec_tests_do_not_add_unapproved_custom_no_base_overrides_for_cleanup_inventory()
    {
        var sourceRoot = LocateSourceRoot();
        var cleanupFiles = ThinOverrideCleanupFiles().ToList();
        var missingFiles = MissingThinOverrideCleanupFiles(sourceRoot, cleanupFiles).ToList();

        Assert.Empty(missingFiles);

        var offenders = cleanupFiles
            .Select(path => Path.Combine(sourceRoot, path))
            .SelectMany(path => FindCustomNoBaseOverrides(sourceRoot, path, File.ReadAllText(path)))
            .Where(offender => ThinOverrideCleanupMethodNames.Contains(MethodName(offender)))
            .Where(offender => !AllowedCustomNoBaseOverrides.Contains(offender))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [ConditionalFact]
    public void Spec_tests_do_not_add_known_custom_execution_logic_for_cleanup_inventory()
    {
        var sourceRoot = LocateSourceRoot();
        var cleanupFiles = ThinOverrideCleanupFiles().ToList();
        var missingFiles = MissingThinOverrideCleanupFiles(sourceRoot, cleanupFiles).ToList();

        Assert.Empty(missingFiles);

        var offenders = cleanupFiles
            .Select(path => Path.Combine(sourceRoot, path))
            .SelectMany(path => FindCustomExecutionLogicOverrides(
                sourceRoot,
                path,
                File.ReadAllText(path)))
            .Where(offender => ThinOverrideCleanupMethodNames.Contains(MethodName(offender)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [ConditionalFact]
    public void Thin_override_cleanup_inventory_reports_missing_files()
    {
        var missingFiles = MissingThinOverrideCleanupFiles(
                "/repo",
                ["PresentDynamoTest.cs", "MissingDynamoTest.cs"],
                fileExists: path => path.EndsWith("PresentDynamoTest.cs", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["MissingDynamoTest.cs"], missingFiles);
    }

    [ConditionalFact]
    public void Skipped_no_op_override_detection_handles_block_returns_and_comments()
    {
        const string Source = """
                              public class SampleDynamoTest
                              {
                                  [ConditionalFact(Skip = SkipReason.NotSupported)]
                                  public override Task Empty_block_with_comment()
                                  {
                                      // DynamoDB cannot support this shape.
                                      return Task.CompletedTask;
                                  }

                                  [ConditionalFact(Skip = SkipReason.NotSupported)]
                                  public override Task Calls_base()
                                  {
                                      return base.Calls_base();
                                  }
                              }
                              """;

        var offenders = FindSkippedNoOpOverrides("/repo", "/repo/SampleDynamoTest.cs", Source)
            .ToList();

        Assert.Equal(["SampleDynamoTest.cs: Empty_block_with_comment"], offenders);
    }

    [ConditionalFact]
    public void Custom_no_base_override_detection_handles_expression_and_block_bodies()
    {
        const string Source = """
                              public class SampleDynamoTest
                              {
                                  public override Task Calls_base()
                                      => base.Calls_base();

                                  public override Task Calls_line_wrapped_base()
                                      => base
                                          .Calls_line_wrapped_base();

                                  public override Task Custom_body()
                                  {
                                      return Task.CompletedTask;
                                  }
                              }
                              """;

        var offenders = FindCustomNoBaseOverrides("/repo", "/repo/SampleDynamoTest.cs", Source)
            .ToList();

        Assert.Equal(["SampleDynamoTest.cs: Custom_body"], offenders);
    }

    [ConditionalFact]
    public void Custom_execution_logic_detection_handles_base_plus_custom_query()
    {
        const string Source = """
                              public class SampleDynamoTest
                              {
                                  public override Task Calls_base()
                                      => base.Calls_base();

                                  public override async Task Custom_query_then_base()
                                  {
                                      using var context = CreateContext();
                                      await context.Set<Customer>().ToListAsync();
                                      await base.Custom_query_then_base();
                                  }
                              }
                              """;

        var offenders =
            FindCustomExecutionLogicOverrides("/repo", "/repo/SampleDynamoTest.cs", Source)
                .ToList();

        Assert.Equal(["SampleDynamoTest.cs: Custom_query_then_base"], offenders);
    }

    [ConditionalFact]
    public void Override_body_detection_handles_deeply_nested_custom_logic()
    {
        const string Source = """
                              public class SampleDynamoTest
                              {
                                  public override async Task Custom_query_then_base()
                                  {
                                      using (var context = CreateContext())
                                      {
                                          if (await context.Set<Customer>().CountAsync() > 0)
                                          {
                                              Assert.Equal(1, await context.Set<Customer>().SingleAsync(c => c.Id == 1));
                                          }
                                      }

                                      await base.Custom_query_then_base();
                                  }
                              }
                              """;

        var offenders =
            FindCustomExecutionLogicOverrides("/repo", "/repo/SampleDynamoTest.cs", Source)
                .ToList();

        Assert.Equal(["SampleDynamoTest.cs: Custom_query_then_base"], offenders);
    }

    protected override Assembly TargetAssembly { get; } = typeof(ComplianceDynamoTest).Assembly;

    private static readonly ISet<string> AllowedCustomNoBaseOverrides =
        new HashSet<string>(StringComparer.Ordinal);

    // Guard the finite cleanup inventory from this refactor. Broader override policy is enforced by
    // Spec_tests_do_not_add_skipped_no_op_overrides and each class' Check_all_tests_overridden
    // guard.
    private static readonly ISet<string> ThinOverrideCleanupMethodNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Can_insert_and_read_back_with_binary_key",
            "Can_insert_and_read_back_with_string_key",
            "Can_read_back_mapped_enum_from_collection_first_or_default",
            "Can_read_back_bool_mapped_as_int_through_navigation",
            "Can_query_and_update_with_nullable_converter_on_unique_index",
            "Can_query_and_update_with_conversion_for_custom_type",
            "Can_query_and_update_with_conversion_for_custom_struct",
            "Can_insert_and_read_back_with_string_list",
            "Can_insert_and_query_struct_to_string_converter_for_pk",
            "Collection_property_as_scalar_Count_member",
            "Optional_owned_with_converter_reading_non_nullable_column",
            "Composition_over_collection_of_complex_mapped_as_scalar",
            "First",
            "SaveChanges",
            "ToList",
            "Can_null_complex_property_with_default_values_and_multiple_properties",
            "Only_one_part_of_a_composite_key_needs_to_vary_for_uniqueness",
            "Seeding_does_not_leave_context_contaminated",
            "Can_get_current_values",
            "Entity_added_to_state_manager",
            "Entity_reverts_when_state_set_to_unchanged",
            "Multiple_entities_can_revert",
            "Entity_does_not_revert_when_attached_on_DbContext",
            "Entity_does_not_revert_when_attached_on_DbSet",
            "Entity_range_does_not_revert_when_attached_dbContext",
            "Entity_range_does_not_revert_when_attached_dbSet",
            "Can_disable_and_reenable_query_result_tracking",
            "Can_disable_and_reenable_query_result_tracking_starting_with_NoTracking",
            "Can_disable_and_reenable_query_result_tracking_query_caching",
            "Can_disable_and_reenable_query_result_tracking_query_caching_using_options",
            "Can_disable_and_reenable_query_result_tracking_query_caching_single_context",
            "AsTracking_switches_tracking_on_when_off_in_options",
            "Precedence_of_tracking_modifiers",
            "Precedence_of_tracking_modifiers2",
            "Client_eval",
            "Single_query_tag",
            "Single_query_multiple_tags",
            "Duplicate_tags",
            "Tag_on_scalar_query",
            "Single_query_multiline_tag",
            "Single_query_multiple_multiline_tag",
            "Single_query_multiline_tag_with_empty_lines",
            "String_starts_with_on_argument_with_bracket",
            "Select_associate_collection",
            "Select_nested_collection_on_required_associate",
            "Select_nested_collection_on_optional_associate",
            "Select_non_nullable_value_type",
            "Select_nullable_value_type",
            "Select_nullable_value_type_with_Value",
            "Equality_in_query_with_parameter",
            "Equality_in_query_with_constant"
        };

    private static string MethodName(string offender)
        => offender[(offender.LastIndexOf(' ') + 1)..];

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

    private static IEnumerable<string> ThinOverrideCleanupFiles()
    {
        yield return "BuiltInDataTypesDynamoTest.cs";
        yield return "ConvertToProviderTypesDynamoTest.cs";
        yield return "CustomConvertersDynamoTest.cs";
        yield return "KeysWithConvertersDynamoTest.cs";
        yield return "ValueConvertersEndToEndDynamoTest.cs";
        yield return "ConcurrencyDetectorEnabledDynamoTest.cs";
        yield return "ConcurrencyDetectorDisabledDynamoTest.cs";
        yield return "ComplexTypesTrackingDynamoTest.cs";
        yield return "CompositeKeyEndToEndDynamoTest.cs";
        yield return "SeedingDynamoTest.cs";
        yield return "Query/NorthwindAsNoTrackingQueryDynamoTest.cs";
        yield return "Query/NorthwindAsTrackingQueryDynamoTest.cs";
        yield return "Query/NorthwindChangeTrackingQueryDynamoTest.cs";
        yield return "Query/NorthwindQueryFiltersQueryDynamoTest.cs";
        yield return "Query/NorthwindQueryTaggingQueryDynamoTest.cs";
        yield return
            "Query/Associations/ComplexProperties/ComplexPropertiesProjectionDynamoTest.cs";
        yield return "Query/FunkyDataQueryDynamoTest.cs";
        yield return "Query/PrimitiveCollectionsQueryDynamoTest.cs";

        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(LocateSourceRoot(), "Types"),
            "Dynamo*TypeTest.cs"))
            yield return Path.GetRelativePath(LocateSourceRoot(), path);
    }

    private static IEnumerable<string> MissingThinOverrideCleanupFiles(
        string sourceRoot,
        IEnumerable<string> relativePaths,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        return relativePaths
            .Where(path => !fileExists(Path.Combine(sourceRoot, path)))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> FindSkippedNoOpOverrides(
        string sourceRoot,
        string path,
        string source)
    {
        var pattern = new Regex(
            @"\[Conditional(?:Fact|Theory)\([^\]]*Skip\s*=\s*[^\]]+\)\][\s\S]*?public\s+override\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void|\w+)\s+(?<method>\w+)\s*\([^)]*\)[\s\S]*?(?:=>\s*Task\.CompletedTask\s*;|=>\s*default\s*;|\{\s*(?:(?://[^\r\n]*(?:\r?\n)?|/\*[\s\S]*?\*/)\s*)*(?:return\s+Task\.CompletedTask\s*;|return\s*;)?\s*\})",
            RegexOptions.Multiline);

        foreach (Match match in pattern.Matches(source))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, path);
            yield return $"{relativePath}: {match.Groups["method"].Value}";
        }
    }

    private static IEnumerable<string> FindCustomNoBaseOverrides(
        string sourceRoot,
        string path,
        string source)
    {
        foreach (var (method, body) in FindOverrideBodies(source))
        {
            if (Regex.IsMatch(body, @"\bbase\s*\.", RegexOptions.CultureInvariant))
                continue;

            var relativePath = Path.GetRelativePath(sourceRoot, path);
            yield return $"{relativePath}: {method}";
        }
    }

    private static IEnumerable<string> FindCustomExecutionLogicOverrides(
        string sourceRoot,
        string path,
        string source)
    {
        string[] customExecutionTokens =
        [
            "CreateContext(",
            ".ToListAsync(",
            ".SingleAsync(",
            ".CountAsync(",
            ".AsAsyncEnumerable(",
            ".AllowScan(",
            ".AsUnsafeFilteredQuery(",
            "context.Add(",
            "Assert.Equal",
            "Assert.Throws",
            "ExecuteWithStrategyInTransactionAsync"
        ];

        foreach (var (method, body) in FindOverrideBodies(source))
        {
            if (!customExecutionTokens.Any(token => body.Contains(token, StringComparison.Ordinal)))
                continue;

            var relativePath = Path.GetRelativePath(sourceRoot, path);
            yield return $"{relativePath}: {method}";
        }
    }

    private static IEnumerable<(string Method, string Body)> FindOverrideBodies(string source)
    {
        var signaturePattern = new Regex(
            @"public\s+override\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void|\w+)\s+(?<method>\w+)\s*\([^)]*\)",
            RegexOptions.Multiline);

        foreach (Match match in signaturePattern.Matches(source))
        {
            var bodyStart = SkipWhitespace(source, match.Index + match.Length);
            var bodyEnd =
                bodyStart < source.Length
                && source[bodyStart] == '='
                && bodyStart + 1 < source.Length
                && source[bodyStart + 1] == '>'
                    ? FindExpressionBodyEnd(source, bodyStart)
                    : FindBlockBodyEnd(source, bodyStart);

            if (bodyEnd < 0)
                continue;

            yield return (match.Groups["method"].Value, source[bodyStart..bodyEnd]);
        }
    }

    private static int SkipWhitespace(string source, int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index]))
            index++;

        return index;
    }

    private static int FindExpressionBodyEnd(string source, int start)
    {
        var end = source.IndexOf(';', start);
        return end < 0 ? -1 : end + 1;
    }

    private static int FindBlockBodyEnd(string source, int start)
    {
        if (start >= source.Length || source[start] != '{')
            return -1;

        var depth = 0;
        for (var i = start; i < source.Length; i++)
            switch (source[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return i + 1;
                    break;
            }

        return -1;
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
        yield return typeof(TypeTestBase<,>);
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
