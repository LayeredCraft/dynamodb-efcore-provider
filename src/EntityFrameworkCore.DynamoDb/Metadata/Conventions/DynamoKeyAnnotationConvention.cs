using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     A model-finalizing convention that finalizes DynamoDB entity key-mapping annotations.
/// </summary>
/// <remarks>
///     Runs after key discovery so Dynamo-specific key annotations and Dynamo naming conventions can be
///     resolved consistently. Annotation precedence is:
///     <list type="number">
///         <item>Explicit/DataAnnotation key-mapping annotations already present on the entity.</item>
///         <item>Conventional property names (<c>PK</c>/<c>PartitionKey</c>, <c>SK</c>/<c>SortKey</c>).</item>
///     </list>
///     <para>
///         EF primary keys are validated against the resolved DynamoDB key schema, but <c>HasKey(...)</c>
///         is not used to infer DynamoDB partition or sort keys.
///     </para>
///     <para>
///         If an entity type has multiple properties whose names match the same role (e.g., both
///         <c>PK</c> and <c>PartitionKey</c>) and no explicit override has been provided, an
///         <c>InvalidOperationException</c> is raised. Resolve the ambiguity by calling
///         <c>HasPartitionKey()</c> or <c>HasSortKey()</c>.
///     </para>
/// </remarks>
public sealed class DynamoKeyAnnotationConvention : IModelFinalizingConvention
{
    /// <summary>
    ///     Finalizes partition/sort key annotations by applying configured mappings, EF-primary-key
    ///     fallback, and conventional property-name fallback in precedence order.
    /// </summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.BaseType != null)
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
    ///     <c>ConfigurationSource.Explicit</c> or
    ///     <c>ConfigurationSource.DataAnnotation</c> source.
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
