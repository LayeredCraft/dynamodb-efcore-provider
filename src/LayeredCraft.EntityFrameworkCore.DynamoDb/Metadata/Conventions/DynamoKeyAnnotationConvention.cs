using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     A model-finalizing convention that sets DynamoDB partition/sort key annotations from
///     conventional property names and validates that no ambiguous name mappings remain.
/// </summary>
/// <remarks>
///     Runs after key discovery so that the EF primary key is already established when annotations are
///     written. Separation of concerns: <see cref="DynamoKeyDiscoveryConvention" /> owns key list
///     manipulation; this convention owns annotation state and conflict validation.
///     <para>
///         Annotation-setting uses <see cref="ConfigurationSource.Convention" /> so that explicit
///         <c>HasPartitionKey</c> / <c>HasSortKey</c> calls (which write at
///         <see cref="ConfigurationSource.Explicit" />) always win.
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
    ///     Sets partition/sort key annotations from conventional property names and validates that no
    ///     unresolved ambiguities exist.
    /// </summary>
    /// <param name="modelBuilder">The convention model builder.</param>
    /// <param name="context">The convention context.</param>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.FindOwnership() != null)
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
        }
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
