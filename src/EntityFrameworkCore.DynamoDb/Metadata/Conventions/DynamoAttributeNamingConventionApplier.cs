using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Applies per-entity attribute naming conventions to all declared scalar properties and complex
///     properties during model finalization.
/// </summary>
/// <remarks>
///     <para>
///         Reads the <see cref="DynamoNamingConventionDescriptor" /> stored as a runtime annotation
///         on each entity type and writes convention-source <c>AttributeName</c> annotations on
///         properties and complex properties that do not already have an explicit name configured.
///     </para>
///     <para>
///         Priority order: explicit <c>HasAttributeName()</c> (Explicit/DataAnnotation source) &gt;
///         naming convention (Convention source). When no per-entity convention is configured, the
///         provider default convention is <c>CamelCase</c>.
///     </para>
///     <para>
///         Complex properties and their nested scalar/complex properties inherit the naming
///         convention from the root entity type.
///     </para>
///     <para>
///         This convention must run before <see cref="DynamoKeyAnnotationConvention" /> so that
///         partition/sort key attribute names are already transformed when key validation reads them.
///     </para>
/// </remarks>
public sealed class DynamoAttributeNamingConventionApplier : IModelFinalizingConvention
{
    private static readonly DynamoNamingConventionDescriptor DefaultDescriptor =
        DynamoNamingConventionDescriptor.Named(DynamoAttributeNamingConvention.CamelCase);

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

            foreach (var property in entityType.GetDeclaredProperties())
            {
                // Skip provider-internal properties that are never persisted as
                // user-facing DynamoDB item attributes.
                if (property.IsRuntimeOnly())
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

            ApplyComplexPropertiesConvention(entityType, descriptor);
        }
    }

    /// <summary>
    ///     Recursively applies the naming convention to all complex properties on a type base
    ///     (entity type or complex type), including the complex property's own attribute name
    ///     (the map key) and all leaf scalar properties.
    /// </summary>
    /// <remarks>
    ///     Uses <see cref="IConventionTypeBase.GetComplexProperties" /> (not
    ///     <c>GetDeclaredComplexProperties</c>) so that when this method is called for a derived
    ///     entity type, complex properties inherited from base entity types are also re-processed
    ///     with the derived type's naming descriptor. Entity types are processed in topological
    ///     order (base before derived) by <see cref="ProcessModelFinalizing" />, so the derived
    ///     descriptor correctly overwrites the base descriptor on shared property objects.
    ///     For complex types there is no inheritance, so this is equivalent to
    ///     <c>GetDeclaredComplexProperties</c>.
    /// </remarks>
    private static void ApplyComplexPropertiesConvention(
        IConventionTypeBase typeBase,
        DynamoNamingConventionDescriptor descriptor)
    {
        foreach (var cp in typeBase.GetComplexProperties())
        {
            // Apply naming convention to the complex property itself (the DynamoDB map key).
            var cpSource = cp.GetAttributeNameConfigurationSource();
            if (cpSource is not ConfigurationSource.Explicit
                and not ConfigurationSource.DataAnnotation)
                cp.SetAttributeName(descriptor.Translate(cp.Name), fromDataAnnotation: false);

            // Apply naming convention to scalar leaf properties inside the complex type.
            foreach (var lp in cp.ComplexType.GetDeclaredProperties())
            {
                if (lp.IsRuntimeOnly())
                    continue;

                var lpSource =
                    lp
                        .FindAnnotation(DynamoAnnotationNames.AttributeName)
                        ?.GetConfigurationSource();

                if (lpSource is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation)
                    continue;

                lp.Builder.HasAttributeName(descriptor.Translate(lp.Name), false);
            }

            // Recurse into nested complex types.
            ApplyComplexPropertiesConvention(cp.ComplexType, descriptor);
        }
    }

    /// <summary>
    ///     Resolves the naming convention descriptor for an entity type, walking up the base-type
    ///     chain so that a convention set on a root entity propagates to derived types.
    /// </summary>
    private static DynamoNamingConventionDescriptor ResolveDescriptor(
        IConventionEntityType entityType)
    {
        var current = entityType;
        while (current is not null)
        {
            if (current.FindAnnotation(DynamoAnnotationNames.AttributeNamingConvention)?.Value is
                DynamoNamingConventionDescriptor descriptor)
                return descriptor;

            current = current.BaseType;
        }

        return DefaultDescriptor;
    }
}
