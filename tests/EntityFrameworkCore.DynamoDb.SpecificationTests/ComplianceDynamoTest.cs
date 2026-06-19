using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class ComplianceDynamoTest : ComplianceTestBase
{
    protected override Assembly TargetAssembly { get; } = typeof(ComplianceDynamoTest).Assembly;

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
        yield return typeof(ConcurrencyDetectorDisabledTestBase<>);
        yield return typeof(ConcurrencyDetectorEnabledTestBase<>);
        yield return typeof(FindTestBase<>);
        yield return typeof(EnumTranslationsTestBase<>);
        yield return typeof(GuidTranslationsTestBase<>);
        yield return typeof(StringTranslationsTestBase<>);
        yield return typeof(KeysWithConvertersTestBase<>);
        yield return typeof(LoggingTestBase);
        yield return typeof(MaterializationInterceptionTestBase<>);
        yield return typeof(OverzealousInitializationTestBase<>);
        yield return typeof(QueryExpressionInterceptionTestBase);
        yield return typeof(SaveChangesInterceptionTestBase);
        yield return typeof(SeedingTestBase);
        yield return typeof(ValueConvertersEndToEndTestBase<>);
        yield return typeof(ComplexPropertiesStructuralEqualityTestBase<>);
        yield return typeof(ComparisonOperatorTranslationsTestBase<>);
        yield return typeof(LogicalOperatorTranslationsTestBase<>);
        yield return typeof(NorthwindAsNoTrackingQueryTestBase<>);
        yield return typeof(NorthwindAsTrackingQueryTestBase<>);
        yield return typeof(NorthwindChangeTrackingQueryTestBase<>);
        yield return typeof(NorthwindFunctionsQueryTestBase<>);
        yield return typeof(NorthwindQueryTaggingQueryTestBase<>);
        yield return typeof(NorthwindSelectQueryTestBase<>);
        yield return typeof(NorthwindWhereQueryTestBase<>);
    }
}
