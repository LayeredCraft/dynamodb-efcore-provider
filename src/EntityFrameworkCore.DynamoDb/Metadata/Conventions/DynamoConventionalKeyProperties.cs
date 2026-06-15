using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Helper for DynamoDB conventional key-name matching.</summary>
/// <remarks>
///     The active provider convention is <see cref="DynamoTableKeyResolutionConvention" />. Final key
///     resolution supports provider APIs, EF <c>HasKey</c>, <c>[Key]</c>, <c>[PrimaryKey]</c>, mapped
///     shadow keys, and conventional <c>PK</c>/<c>PartitionKey</c>/<c>Id</c> plus
///     <c>SK</c>/<c>SortKey</c> names.
/// </remarks>
internal static class DynamoConventionalKeyProperties
{
    /// <summary>Returns true when the property can be discovered as a conventional DynamoDB key.</summary>
    internal static bool IsDiscoverableKeyProperty(IConventionProperty property)
        => !property.IsShadowProperty() && !property.IsRuntimeOnly();

    /// <summary>
    ///     Gets conventional partition key candidates, preferring DynamoDB-specific names over the
    ///     fallback <c>Id</c> name.
    /// </summary>
    internal static List<IConventionProperty> GetPartitionKeyCandidates(
        IEnumerable<IConventionProperty> properties)
    {
        var dynamoCandidates = new List<IConventionProperty>();
        var fallbackCandidates = new List<IConventionProperty>();

        foreach (var property in properties)
            if (IsPartitionKeyName(property.Name))
                dynamoCandidates.Add(property);
            else if (IsPartitionKeyFallbackName(property.Name))
                fallbackCandidates.Add(property);

        return dynamoCandidates.Count > 0 ? dynamoCandidates : fallbackCandidates;
    }

    /// <summary>Returns true when the property name matches a DynamoDB-specific partition key convention.</summary>
    internal static bool IsPartitionKeyName(string name)
        => string.Equals(name, "PK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "PartitionKey", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the property name matches the fallback partition key convention.</summary>
    internal static bool IsPartitionKeyFallbackName(string name)
        => string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the property name matches a DynamoDB sort key convention.</summary>
    internal static bool IsSortKeyName(string name)
        => string.Equals(name, "SK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SortKey", StringComparison.OrdinalIgnoreCase);
}
