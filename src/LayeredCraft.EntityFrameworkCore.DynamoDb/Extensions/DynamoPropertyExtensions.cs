// ReSharper disable CheckNamespace

using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

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
    }

    extension(IMutableProperty property)
    {
        /// <summary>Sets or clears the DynamoDB attribute name override for this property.</summary>
        public void SetAttributeName(string? name)
            => property.SetOrRemoveAnnotation(DynamoAnnotationNames.AttributeName, name);
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
    }
}
