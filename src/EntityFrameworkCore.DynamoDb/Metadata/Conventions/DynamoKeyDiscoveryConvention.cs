using EntityFrameworkCore.DynamoDb.Metadata.Internal;
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
///             <c>SK</c> or <c>SortKey</c> — designated as the DynamoDB sort key and appended after
///             the partition key in the EF primary key.
///         </item>
///     </list>
///     Convention-level discovery is overridden by explicit <c>HasPartitionKey</c> or
///     <c>HasSortKey</c> fluent API calls, which set annotations at
///     <c>Microsoft.EntityFrameworkCore.Metadata.ConfigurationSource.Explicit</c> source and
///     are handled by <c>DynamoKeyInPrimaryKeyConvention</c>. Root entity types without a
///     conventional partition key name do not fall back to EF Core's <c>Id</c>/<c>[Key]</c> discovery.
///     Annotation-setting and ambiguity validation are handled separately by
///     <c>DynamoKeyAnnotationConvention</c>.
///     <para>
///         For owned collection element types (<c>OwnsMany</c>), EF Core 10's base
///         <c>DiscoverKeyProperties</c> calls <c>CreateUniqueProperty(int, "Id")</c> when no
///         naming-convention candidates are found, producing a shadow int property named "Id".
///         If the user later explicitly configures a CLR property also named "Id" (e.g.,
///         <c>results.Property(r =&gt; r.Id).HasAttributeName("id")</c>), the shadow and CLR
///         properties conflict, triggering repeated convention invocations until EF Core hits
///         its recursion limit. This convention overrides <c>DiscoverKeyProperties</c> for
///         <c>OwnsMany</c> types to bypass that path entirely; ordinal shadow key creation
///         is left to <c>OwnedTypePrimaryKeyConvention</c> during model finalization.
///     </para>
/// </remarks>
public class DynamoKeyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
    : KeyDiscoveryConvention(dependencies)
{
    /// <summary>
    ///     Returns the candidate key properties for the entity type, bypassing EF Core's
    ///     naming-convention discovery for owned collection elements.
    /// </summary>
    /// <remarks>
    ///     For <c>OwnsMany</c> entity types, returns the ownership FK properties plus an ordinal
    ///     shadow int property. If no ordinal exists yet, one is created immediately using the
    ///     <c>__OwnedOrdinal</c> base name via <c>CreateUniqueProperty</c>. Creating the ordinal
    ///     here (during model building) rather than in <c>OwnedTypePrimaryKeyConvention</c>
    ///     (finalization) avoids exceeding EF Core's convention-invocation recursion limit.
    ///     Using a non-<c>Id</c> base name prevents conflicts with user-defined CLR <c>Id</c>
    ///     properties that would otherwise cause convention ping-pong when EF Core's base
    ///     implementation calls <c>CreateUniqueProperty(int, "Id")</c>.
    ///     For all other entity types (root, <c>OwnsOne</c>), delegates to the base implementation.
    /// </remarks>
    protected override List<IConventionProperty>? DiscoverKeyProperties(IConventionEntityType entityType)
    {
        if (entityType.IsOwned())
        {
            var ownership = entityType.FindOwnership();
            if (ownership?.DeclaringEntityType == entityType && !ownership.IsUnique)
            {
                var keyProps = ownership.Properties.ToList();
                var ordinal = entityType.GetProperties()
                    .FirstOrDefault(p => p[DynamoAnnotationNames.OwnedOrdinalKey] as bool? == true);
                if (ordinal != null)
                {
                    keyProps.Add(ordinal);
                    return keyProps;
                }

                // No provider-stamped ordinal yet — create one using a name that cannot conflict
                // with user-defined CLR or shadow "Id" properties. Stamp the annotation immediately
                // so subsequent calls (triggered by IPropertyAddedConvention) find it and return
                // without recursing further. Creating it here (during model building, not
                // finalization) keeps total convention invocations within EF Core's recursion limit.
                var ordinalBuilder = entityType.Builder.CreateUniqueProperty(typeof(int), "__OwnedOrdinal", true);
                if (ordinalBuilder == null)
                    throw new InvalidOperationException(
                        $"The DynamoDB provider could not create the ordinal shadow key property for owned " +
                        $"collection element type '{entityType.ClrType.Name}'. The entity type builder " +
                        $"rejected the property, which would produce a primary key containing only the " +
                        $"foreign key properties and break change tracking for the owned collection.");

                ordinalBuilder.HasAnnotation(DynamoAnnotationNames.OwnedOrdinalKey, true);
                keyProps.Add(ordinalBuilder.Metadata);
                return keyProps;
            }
        }

        return base.DiscoverKeyProperties(entityType);
    }

    /// <summary>Configures EF key candidates from DynamoDB conventional-name properties.</summary>
    /// <remarks>
    ///     For root entity types, key discovery requires a conventional partition key property (
    ///     <c>PK</c> / <c>PartitionKey</c>). If a conventional sort key property is also present (
    ///     <c>SK</c> / <c>SortKey</c>), it is appended after the partition key. Root types without a
    ///     conventional partition key leave key discovery empty so EF does not implicitly promote
    ///     <c>Id</c> as the table key. Annotations are not set here — that is the responsibility of
    ///     <c>DynamoKeyAnnotationConvention</c>.
    ///     For owned entity types, delegates to the base implementation (deduplicate only), since
    ///     <c>DiscoverKeyProperties</c> already returns the correct FK + ordinal list.
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
    ///     Returns  when the property name matches a DynamoDB partition key
    ///     convention.
    /// </summary>
    internal static bool IsPartitionKeyName(string name)
        => string.Equals(name, "PK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "PartitionKey", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Returns  when the property name matches a DynamoDB sort key
    ///     convention.
    /// </summary>
    internal static bool IsSortKeyName(string name)
        => string.Equals(name, "SK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SortKey", StringComparison.OrdinalIgnoreCase);
}
