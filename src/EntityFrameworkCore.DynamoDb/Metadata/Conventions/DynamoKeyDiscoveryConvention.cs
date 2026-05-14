using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>A convention that discovers DynamoDB table keys from CLR property naming conventions.</summary>
/// <remarks>
///     Inherits <c>KeyDiscoveryConvention</c> so that all standard EF Core key-discovery
///     triggers (entity type added, property added, key removed, foreign key ownership changes, etc.)
///     are handled by a single replacement convention rather than running alongside the base. The
///     following property names are recognised (case-insensitive, ordinal comparison):
///     <list type="bullet">
///         <item>
///             <c>PK</c> or <c>PartitionKey</c> — designated as the DynamoDB partition key and
///             placed first in the EF primary key.
///         </item>
///         <item>
///             <c>Id</c> — designated as the DynamoDB partition key only when no
///             DynamoDB-specific partition key name exists.
///         </item>
///         <item>
///             <c>SK</c> or <c>SortKey</c> — designated as the DynamoDB sort key and appended after
///             the partition key in the EF primary key.
///         </item>
///     </list>
///     Convention-level discovery is overridden by explicit <c>HasPartitionKey</c> or
///     <c>HasSortKey</c> fluent API calls, which set annotations at
///     <c>Microsoft.EntityFrameworkCore.Metadata.ConfigurationSource.Explicit</c> source and
///     are handled by <c>DynamoKeyInPrimaryKeyConvention</c>. Root entity types fall back to an
///     <c>Id</c> property for the partition key only when no <c>PK</c> or <c>PartitionKey</c>
///     property exists. They never infer DynamoDB table keys from <c>[Key]</c>.
///     Annotation-setting and ambiguity validation are handled separately by
///     <c>DynamoKeyAnnotationConvention</c>.
/// </remarks>
public class DynamoKeyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
    : KeyDiscoveryConvention(dependencies)
{
    /// <summary>Configures EF key candidates from DynamoDB conventional-name properties.</summary>
    /// <remarks>
    ///     Key discovery first looks for a DynamoDB-specific partition key property (<c>PK</c> /
    ///     <c>PartitionKey</c>), then falls back to <c>Id</c> when no DynamoDB-specific name exists.
    ///     If a conventional sort key property is also present (<c>SK</c> / <c>SortKey</c>), it is
    ///     appended after the partition key. Annotations are not set here — that is the responsibility
    ///     of <c>DynamoKeyAnnotationConvention</c>.
    /// </remarks>
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
