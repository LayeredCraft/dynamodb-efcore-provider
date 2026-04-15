// ReSharper disable CheckNamespace

using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoPropertyExtensions type.</summary>
public static class DynamoPropertyExtensions
{
    extension(IReadOnlyProperty property)
    {
        /// <summary>Determines whether this property is the ordinal key part for an owned collection element.</summary>
        public bool IsOwnedOrdinalKeyProperty()
        {
            if (property.DeclaringType is not IReadOnlyEntityType entityType)
                return false;

            var ownership = entityType.FindOwnership();
            if (ownership == null || ownership.IsUnique)
                return false;

            if (property.ClrType != typeof(int) || !property.IsPrimaryKey())
                return false;

            var fkProperties = ownership.Properties;
            return !fkProperties.Contains(property);
        }

        /// <summary>
        ///     Returns the DynamoDB attribute name for this property, falling back to the CLR property
        ///     name.
        /// </summary>
        public string GetAttributeName()
            => (string?)property[DynamoAnnotationNames.AttributeName] ?? property.Name;

        /// <summary>Returns whether this property is runtime-only provider metadata.</summary>
        public bool IsRuntimeOnly()
            => property[DynamoAnnotationNames.RuntimeOnlyProperty] as bool? == true;
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
    }
}
