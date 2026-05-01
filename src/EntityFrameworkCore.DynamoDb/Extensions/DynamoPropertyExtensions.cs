// ReSharper disable CheckNamespace

using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoPropertyExtensions type.</summary>
public static class DynamoPropertyExtensions
{
    extension(IReadOnlyProperty property)
    {
        /// <summary>
        ///     Returns the DynamoDB attribute name for this property, falling back to the CLR property
        ///     name.
        /// </summary>
        public string GetAttributeName()
            => (string?)property[DynamoAnnotationNames.AttributeName] ?? property.Name;

        /// <summary>Returns whether this property is runtime-only provider metadata.</summary>
        public bool IsRuntimeOnly()
            => property[DynamoAnnotationNames.RuntimeOnlyProperty] as bool? == true;

        /// <summary>Gets the runtime value source identifier for a runtime-only property.</summary>
        public string? GetRuntimeValueSource()
            => property[DynamoAnnotationNames.RuntimeValueSource] as string;
    }

    extension(IMutableProperty property)
    {
        /// <summary>Sets or clears the DynamoDB attribute name override for this property.</summary>
        public void SetAttributeName(string? name)
            => property.SetOrRemoveAnnotation(DynamoAnnotationNames.AttributeName, name);

        /// <summary>Marks this property as runtime-only provider metadata.</summary>
        public void SetRuntimeOnly(bool runtimeOnly)
            => property.SetOrRemoveAnnotation(
                DynamoAnnotationNames.RuntimeOnlyProperty,
                runtimeOnly ? true : null);

        /// <summary>Sets or clears the runtime value source identifier for this property.</summary>
        public void SetRuntimeValueSource(string? runtimeValueSource)
            => property.SetOrRemoveAnnotation(
                DynamoAnnotationNames.RuntimeValueSource,
                runtimeValueSource);
    }

    extension(IConventionProperty property)
    {
        /// <summary>Sets the DynamoDB attribute name override, recording the configuration source.</summary>
        public string? SetAttributeName(string? name, bool fromDataAnnotation = false)
            => (string?)property.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.AttributeName,
                    name,
                    fromDataAnnotation)
                ?.Value;

        /// <summary>
        ///     Sets whether this property is runtime-only provider metadata, recording the configuration
        ///     source.
        /// </summary>
        public bool? SetRuntimeOnly(bool runtimeOnly, bool fromDataAnnotation = false)
            => (bool?)property.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.RuntimeOnlyProperty,
                    runtimeOnly ? true : null,
                    fromDataAnnotation)
                ?.Value;

        /// <summary>
        ///     Sets the runtime value source identifier for this property, recording the configuration
        ///     source.
        /// </summary>
        public string? SetRuntimeValueSource(
            string? runtimeValueSource,
            bool fromDataAnnotation = false)
            => (string?)property.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.RuntimeValueSource,
                    runtimeValueSource,
                    fromDataAnnotation)
                ?.Value;
    }

    extension(IReadOnlyComplexProperty complexProperty)
    {
        /// <summary>
        ///     Returns the DynamoDB attribute name for this complex property (the map key in the parent
        ///     document), falling back to the CLR property name.
        /// </summary>
        /// <remarks>
        ///     Reuses <see cref="DynamoAnnotationNames.AttributeName" /> — EF Core annotation storage
        ///     is per-object so there is no collision with <see cref="IReadOnlyProperty" />.
        /// </remarks>
        public string GetAttributeName()
            => (string?)complexProperty[DynamoAnnotationNames.AttributeName]
                ?? complexProperty.Name;
    }

    extension(IMutableComplexProperty complexProperty)
    {
        /// <summary>Sets or clears the DynamoDB attribute name override for this complex property.</summary>
        public void SetAttributeName(string? name)
            => complexProperty.SetOrRemoveAnnotation(DynamoAnnotationNames.AttributeName, name);
    }

    extension(IConventionComplexProperty complexProperty)
    {
        /// <summary>Sets the DynamoDB attribute name for this complex property, recording the configuration source.</summary>
        public string? SetAttributeName(string? name, bool fromDataAnnotation = false)
            => (string?)complexProperty.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.AttributeName,
                    name,
                    fromDataAnnotation)
                ?.Value;

        /// <summary>Returns the configuration source for the complex property attribute name, or null if not set.</summary>
        public ConfigurationSource? GetAttributeNameConfigurationSource()
            => complexProperty
                .FindAnnotation(DynamoAnnotationNames.AttributeName)
                ?.GetConfigurationSource();
    }
}
