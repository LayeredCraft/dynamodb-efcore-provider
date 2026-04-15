using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Applies per-entity attribute naming conventions to all declared scalar properties during
///     model finalization.
/// </summary>
/// <remarks>
///     <para>
///         Reads the <see cref="DynamoNamingConventionDescriptor" /> stored as a runtime annotation
///         on each entity type and writes convention-source <c>AttributeName</c> annotations on
///         properties that do not already have an explicit name configured.
///     </para>
///     <para>
///         Priority order: explicit <c>HasAttributeName()</c> (Explicit/DataAnnotation source) &gt;
///         naming convention (Convention source) &gt; CLR property name fallback.
///     </para>
///     <para>
///         Owned entity types without their own convention inherit the convention from their root
///         entity by walking the ownership chain. Shadow properties (provider-internal) are skipped.
///     </para>
///     <para>
///         This convention must run before <see cref="DynamoKeyAnnotationConvention" /> so that
///         partition/sort key attribute names are already transformed when key validation reads them.
///     </para>
/// </remarks>
public sealed class DynamoAttributeNamingConventionApplier : IModelFinalizingConvention
{
    /// <summary>
    ///     Applies configured naming conventions to all entity types in the model during
    ///     finalization.
    /// </summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var descriptor = ResolveDescriptor(entityType);
            if (descriptor is null)
                continue;

            foreach (var property in entityType.GetDeclaredProperties())
            {
                // Skip provider-internal shadow properties (owned ordinal keys,
                // ExecuteStatementResponse metadata, etc.) — they are not DynamoDB
                // item attributes and should not be renamed.
                if (property.IsShadowProperty())
                    continue;

                // Explicit HasAttributeName() or a data annotation always wins —
                // do not overwrite a higher-priority source.
                var source = property
                    .FindAnnotation(DynamoAnnotationNames.AttributeName)
                    ?.GetConfigurationSource();

                if (source is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation)
                    continue;

                // Write at Convention source; any Explicit/DataAnnotation set earlier or later
                // will override this via EF Core's annotation precedence rules.
                property.Builder.HasAttributeName(descriptor.Translate(property.Name), false);
            }
        }
    }

    /// <summary>
    ///     Resolves the naming convention descriptor for an entity type, walking the ownership chain
    ///     so owned types without their own setting inherit the root entity's convention.
    /// </summary>
    private static DynamoNamingConventionDescriptor? ResolveDescriptor(
        IConventionEntityType entityType)
    {
        var current = (IReadOnlyEntityType)entityType;
        while (current is not null)
        {
            var descriptor =
                current.FindAnnotation(DynamoAnnotationNames.AttributeNamingConvention)?.Value as
                    DynamoNamingConventionDescriptor;

            if (descriptor is not null)
                return descriptor;

            current = current.FindOwnership()?.PrincipalEntityType;
        }

        return null;
    }
}
