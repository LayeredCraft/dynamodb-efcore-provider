using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>A convention that discovers DynamoDB table keys from CLR property naming conventions.</summary>
/// <remarks>
///     Inherits <see cref="KeyDiscoveryConvention" /> so that all standard EF Core key-discovery
///     triggers (entity type added, property added, key removed, foreign key ownership changes, etc.)
///     are handled by a single replacement convention rather than running alongside the base. The
///     following property names are recognised (case-sensitive, ordinal comparison):
///     <list type="bullet">
///         <item>
///             <c>PK</c> or <c>PartitionKey</c> — designated as the DynamoDB partition key and
///             placed first in the EF primary key.
///         </item>
///         <item>
///             <c>SK</c> or <c>SortKey</c> — designated as the DynamoDB sort key and appended after
///             the partition key in the EF primary key.
///         </item>
///     </list>
///     Convention-level discovery is overridden by explicit <c>HasPartitionKey</c> or
///     <c>HasSortKey</c> fluent API calls, which set annotations at
///     <see cref="Microsoft.EntityFrameworkCore.Metadata.ConfigurationSource.Explicit" /> source and
///     are handled by <see cref="DynamoKeyInPrimaryKeyConvention" />. Root entity types without a
///     conventional partition key name do not fall back to EF Core's <c>Id</c>/<c>[Key]</c> discovery.
///     Annotation-setting and ambiguity validation are handled separately by
///     <see cref="DynamoKeyAnnotationConvention" />.
/// </remarks>
public class DynamoKeyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
    : KeyDiscoveryConvention(dependencies)
{
    /// <summary>Configures EF key candidates from DynamoDB conventional-name properties.</summary>
    /// <remarks>
    ///     For root entity types, key discovery requires a conventional partition key property (
    ///     <c>PK</c> / <c>PartitionKey</c>). If a conventional sort key property is also present (
    ///     <c>SK</c> / <c>SortKey</c>), it is appended after the partition key. Root types without a
    ///     conventional partition key leave key discovery empty so EF does not implicitly promote
    ///     <c>Id</c> as the table key. Annotations are not set here — that is the responsibility of
    ///     <see cref="DynamoKeyAnnotationConvention" />.
    /// </remarks>
    protected override void ProcessKeyProperties(
        IList<IConventionProperty> keyProperties,
        IConventionEntityType entityType)
    {
        if (entityType.IsOwned())
        {
            base.ProcessKeyProperties(keyProperties, entityType);
            return;
        }

        var pkProperty = entityType.GetProperties().FirstOrDefault(p => IsPartitionKeyName(p.Name));
        if (pkProperty == null)
        {
            keyProperties.Clear();
            return;
        }

        var skProperty = entityType.GetProperties().FirstOrDefault(p => IsSortKeyName(p.Name));

        keyProperties.Clear();
        keyProperties.Add(pkProperty);

        if (skProperty != null && !keyProperties.Contains(skProperty))
            keyProperties.Add(skProperty);

        base.ProcessKeyProperties(keyProperties, entityType);
    }

    /// <summary>
    ///     Returns <see langword="true" /> when the property name matches a DynamoDB partition key
    ///     convention.
    /// </summary>
    internal static bool IsPartitionKeyName(string name)
        => string.Equals(name, "PK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "PartitionKey", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Returns <see langword="true" /> when the property name matches a DynamoDB sort key
    ///     convention.
    /// </summary>
    internal static bool IsSortKeyName(string name)
        => string.Equals(name, "SK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SortKey", StringComparison.OrdinalIgnoreCase);
}
