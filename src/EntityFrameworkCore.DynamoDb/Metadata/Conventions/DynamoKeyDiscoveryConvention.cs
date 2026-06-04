using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Legacy helper for DynamoDB conventional key-name matching.</summary>
/// <remarks>
///     The active provider convention is <see cref="DynamoTableKeyResolutionConvention" />. This type
///     remains only for shared candidate-selection helpers and compatibility tests. Final key
///     resolution supports provider APIs, EF <c>HasKey</c>, <c>[Key]</c>, <c>[PrimaryKey]</c>, mapped
///     shadow keys, and conventional <c>PK</c>/<c>PartitionKey</c>/<c>Id</c> plus
///     <c>SK</c>/<c>SortKey</c> names.
/// </remarks>
public class DynamoKeyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
    : KeyDiscoveryConvention(dependencies)
{
    /// <inheritdoc />
    protected override bool ShouldDiscoverKeyProperties(IConventionEntityType entityType)
        // DynamoDB keys apply only to root table entities. Owned types are rejected during model
        // finalization; skipping key discovery prevents EF owned-collection shadow-key loops first.
        => !entityType.IsOwned() && base.ShouldDiscoverKeyProperties(entityType);

    /// <summary>Configures EF key candidates from DynamoDB conventional-name properties.</summary>
    protected override void ProcessKeyProperties(
        IList<IConventionProperty> keyProperties,
        IConventionEntityType entityType)
    {
        var properties = entityType.GetProperties().Where(IsDiscoverableKeyProperty).ToList();
        var pkProperty = GetPartitionKeyCandidates(properties).FirstOrDefault();
        if (pkProperty == null)
        {
            keyProperties.Clear();
            return;
        }

        var skProperty = properties.FirstOrDefault(p => IsSortKeyName(p.Name));

        keyProperties.Clear();
        keyProperties.Add(pkProperty);

        if (skProperty != null && !keyProperties.Contains(skProperty))
            keyProperties.Add(skProperty);

        base.ProcessKeyProperties(keyProperties, entityType);
    }

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
