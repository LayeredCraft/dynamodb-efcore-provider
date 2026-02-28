using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     A model-finalizing convention that finalizes DynamoDB entity key-mapping annotations.
/// </summary>
/// <remarks>
///     Runs after key discovery so that EF primary key metadata is available for fallback inference.
///     Annotation precedence is:
///     <list type="number">
///         <item>Explicit/DataAnnotation key-mapping annotations already present on the entity.</item>
///         <item>Explicit/DataAnnotation EF primary key (<c>HasKey(...)</c>).</item>
///         <item>Conventional property names (<c>PK</c>/<c>PartitionKey</c>, <c>SK</c>/<c>SortKey</c>).</item>
///         <item>Convention-discovered EF primary key with one or two properties.</item>
///     </list>
///     <para>
///         This keeps explicit <c>HasKey(...)</c> aligned with entity key mapping while preserving
///         conventional PK/SK discovery when no explicit key shape is configured.
///     </para>
///     <para>
///         If an entity type has multiple properties whose names match the same role (e.g., both
///         <c>PK</c> and <c>PartitionKey</c>) and no explicit override has been provided, an
///         <see cref="InvalidOperationException" /> is raised. Resolve the ambiguity by calling
///         <c>HasPartitionKey()</c> or <c>HasSortKey()</c>.
///     </para>
/// </remarks>
public sealed class DynamoKeyAnnotationConvention : IModelFinalizingConvention
{
    /// <summary>
    ///     Finalizes partition/sort key annotations by applying configured mappings, EF-primary-key
    ///     fallback, and conventional property-name fallback in precedence order.
    /// </summary>
    /// <param name="modelBuilder">The convention model builder.</param>
    /// <param name="context">The convention context.</param>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.IsOwned()
                || entityType.FindOwnership() != null
                || entityType.BaseType != null)
                continue;

            if (HasNonConventionKeyMapping(entityType))
                continue;

            if (TrySetAnnotationsFromPrimaryKey(entityType, true))
                continue;

            SetAnnotationFromConventionalName(
                entityType,
                DynamoAnnotationNames.PartitionKeyPropertyName,
                DynamoKeyDiscoveryConvention.IsPartitionKeyName,
                "partition key",
                "HasPartitionKey");

            SetAnnotationFromConventionalName(
                entityType,
                DynamoAnnotationNames.SortKeyPropertyName,
                DynamoKeyDiscoveryConvention.IsSortKeyName,
                "sort key",
                "HasSortKey");

            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string)
                continue;

            _ = TrySetAnnotationsFromPrimaryKey(entityType, false);
        }
    }

    /// <summary>
    ///     Returns <see langword="true" /> when partition or sort key mapping has already been
    ///     configured using explicit or data-annotation source.
    /// </summary>
    private static bool HasNonConventionKeyMapping(IConventionEntityType entityType)
        => entityType
                    .FindAnnotation(DynamoAnnotationNames.PartitionKeyPropertyName)
                    ?.GetConfigurationSource() is
                ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation
            || entityType
                    .FindAnnotation(DynamoAnnotationNames.SortKeyPropertyName)
                    ?.GetConfigurationSource() is ConfigurationSource.Explicit
                or ConfigurationSource.DataAnnotation;

    /// <summary>Attempts to infer partition/sort key annotations from the EF primary key.</summary>
    /// <param name="entityType">The entity type to update.</param>
    /// <param name="onlyWhenPrimaryKeyIsNonConvention">
    ///     When <see langword="true" />, only
    ///     explicit/data-annotation EF primary keys are considered.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when key annotations were inferred; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool TrySetAnnotationsFromPrimaryKey(
        IConventionEntityType entityType,
        bool onlyWhenPrimaryKeyIsNonConvention)
    {
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey == null)
            return false;

        if (onlyWhenPrimaryKeyIsNonConvention
            && primaryKey.GetConfigurationSource() is not (ConfigurationSource.Explicit
                or ConfigurationSource.DataAnnotation))
            return false;

        if (primaryKey.Properties.Count is < 1 or > 2)
            return false;

        entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.PartitionKeyPropertyName,
            primaryKey.Properties[0].Name);

        entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.SortKeyPropertyName,
            primaryKey.Properties.Count == 2 ? primaryKey.Properties[1].Name : null);

        return true;
    }

    /// <summary>
    ///     Sets the annotation to the single matching property name, or throws if multiple unresolved
    ///     candidates exist. Skips entity types where the annotation was already set at
    ///     <see cref="ConfigurationSource.Explicit" /> or
    ///     <see cref="ConfigurationSource.DataAnnotation" /> source.
    /// </summary>
    private static void SetAnnotationFromConventionalName(
        IConventionEntityType entityType,
        string annotationName,
        Func<string, bool> isConventionalName,
        string roleName,
        string fluentApiName)
    {
        // Explicit or DataAnnotation source means the user already resolved this — skip.
        var source = entityType.FindAnnotation(annotationName)?.GetConfigurationSource();
        if (source is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation)
            return;

        var candidates = entityType.GetProperties().Where(p => isConventionalName(p.Name)).ToList();

        if (candidates.Count == 0)
            return;

        if (candidates.Count > 1)
        {
            var names = string.Join(", ", candidates.Select(p => $"'{p.Name}'"));
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' has multiple properties with conventional "
                + $"{roleName} names ({names}). The convention cannot determine which is the DynamoDB "
                + $"{roleName}. Call {fluentApiName}() to specify the property explicitly.");
        }

        entityType.SetOrRemoveAnnotation(annotationName, candidates[0].Name);
    }
}
