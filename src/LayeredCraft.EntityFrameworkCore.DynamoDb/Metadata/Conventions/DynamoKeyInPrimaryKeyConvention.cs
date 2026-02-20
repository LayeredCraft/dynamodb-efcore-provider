using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     A convention that automatically configures the EF Core primary key to reflect the DynamoDB
///     key schema when <c>HasPartitionKey</c> and/or <c>HasSortKey</c> annotations are set.
/// </summary>
/// <remarks>
///     DynamoDB enforces item uniqueness on the partition key alone (PK-only tables) or on the
///     combination of partition key and sort key (PK+SK tables). This convention ensures the EF
///     primary key always reflects those uniqueness semantics, removing the need for a redundant
///     explicit <c>HasKey</c> call when partition/sort key annotations are configured. Derivation
///     rules:
///     <list type="bullet">
///         <item>
///             If <c>HasPartitionKey</c> is set (and optionally <c>HasSortKey</c>), the EF primary
///             key is rebuilt as <c>[pkProperty]</c> or <c>[pkProperty, skProperty]</c>.
///         </item>
///         <item>
///             If only <c>HasSortKey</c> is set and the entity type has an auto-discovered EF
///             primary key, the first property of that key is used as the implicit partition key and
///             the sort key is appended, forming a composite <c>[pkProperty, skProperty]</c>.
///         </item>
///     </list>
///     The convention only modifies primary keys that were set by convention (auto-discovered by EF).
///     Explicit <c>HasKey</c> calls take precedence; this convention stands down when the primary key
///     has an explicit or data-annotation configuration source. Ordering is always
///     <c>[partitionKeyProperty, sortKeyProperty]</c>, consistent with the fallback derivation in
///     <see cref="Microsoft.EntityFrameworkCore.DynamoEntityTypeExtensions" />.
/// </remarks>
public sealed class DynamoKeyInPrimaryKeyConvention(
    ProviderConventionSetBuilderDependencies dependencies) : IEntityTypeAnnotationChangedConvention
{
    // Dependencies are accepted for consistency with the EF Core convention constructor pattern
    // but are not used directly; all logic is in the static ProcessPrimaryKey helper.
    private readonly ProviderConventionSetBuilderDependencies _dependencies = dependencies;

    /// <inheritdoc />
    public void ProcessEntityTypeAnnotationChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        string name,
        IConventionAnnotation? annotation,
        IConventionAnnotation? oldAnnotation,
        IConventionContext<IConventionAnnotation> context)
    {
        if (name != DynamoAnnotationNames.PartitionKeyPropertyName
            && name != DynamoAnnotationNames.SortKeyPropertyName)
            return;

        ProcessPrimaryKey(entityTypeBuilder);
    }

    /// <summary>
    ///     Rebuilds the EF primary key based on the current DynamoDB partition/sort key annotations,
    ///     if the key is eligible for convention-level modification.
    /// </summary>
    private static void ProcessPrimaryKey(IConventionEntityTypeBuilder entityTypeBuilder)
    {
        var entityType = entityTypeBuilder.Metadata;

        // Only applies to root (non-owned, non-derived) entity types.
        if (entityType.BaseType != null || entityType.IsOwned())
            return;

        var primaryKey = entityType.FindPrimaryKey();

        // Respect explicit HasKey(...) calls — only modify convention-derived keys.
        if (primaryKey != null
            && !ConfigurationSource.Convention.Overrides(primaryKey.GetConfigurationSource()))
            return;

        var pkPropertyName = entityType[DynamoAnnotationNames.PartitionKeyPropertyName] as string;
        var skPropertyName = entityType[DynamoAnnotationNames.SortKeyPropertyName] as string;

        // Neither annotation is set — nothing to do; leave EF key discovery alone.
        if (pkPropertyName == null && skPropertyName == null)
            return;

        // Resolve the partition key property.
        //   1. Explicit HasPartitionKey annotation, if set.
        //   2. Fall back to the first property of the currently-discovered EF primary key
        //      (handles HasSortKey-only when a PK was auto-discovered from property naming).
        IConventionProperty? pkProperty;
        if (pkPropertyName != null)
        {
            pkProperty = entityType.FindProperty(pkPropertyName);
            if (pkProperty == null)
                return; // Property not yet registered; validator will catch this.
        }
        else
        {
            pkProperty = primaryKey?.Properties.FirstOrDefault();
            if (pkProperty == null)
                return; // No EF PK yet; convention cannot act. Validator will catch this.
        }

        var keyProperties = new List<IConventionProperty> { pkProperty };

        if (skPropertyName != null)
        {
            var skProperty = entityType.FindProperty(skPropertyName);
            if (skProperty == null)
                return; // Property not yet registered; validator will catch this.

            if (!keyProperties.Contains(skProperty))
                keyProperties.Add(skProperty);
        }

        // Skip the rebuild if the current EF PK already matches the desired shape.
        if (primaryKey != null && primaryKey.Properties.SequenceEqual(keyProperties))
            return;

        // Rebuild the EF primary key to match the DynamoDB key schema.
        if (primaryKey != null)
            primaryKey.DeclaringEntityType.Builder.HasNoKey(primaryKey);

        entityTypeBuilder.PrimaryKey(keyProperties);
    }
}
