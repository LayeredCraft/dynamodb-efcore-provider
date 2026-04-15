using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Adds an <see cref="ExecuteStatementResponse" /> shadow property to root (non-owned,
///     non-derived) entity types so that per-entity response metadata can be populated during query
///     materialization.
/// </summary>
/// <remarks>
///     The property is named <c>__executeStatementResponse</c> and is marked
///     <see cref="ValueGenerated.OnAddOrUpdate" /> so EF Core treats it as store-managed — it is never
///     serialized to DynamoDB and is excluded from write plans.
/// </remarks>
public sealed class DynamoResponseShadowPropertyConvention : IEntityTypeAddedConvention
{
    internal const string ExecuteStatementResponsePropertyName = "__executeStatementResponse";

    /// <summary>
    ///     Adds the <c>__executeStatementResponse</c> shadow property when a root entity type is
    ///     registered in the model.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <param name="context">The convention context.</param>
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder builder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = builder.Metadata;

        // Only root, non-owned types — owned types share the page that materialized their owner.
        if (entityType.BaseType != null || entityType.IsOwned())
            return;

        var propertyBuilder = builder
            .Property(typeof(ExecuteStatementResponse), ExecuteStatementResponsePropertyName)
            ?.ValueGenerated(ValueGenerated.OnAddOrUpdate);

        propertyBuilder?.Metadata.SetOrRemoveAnnotation(
            DynamoAnnotationNames.RuntimeOnlyProperty,
            true);
    }
}
