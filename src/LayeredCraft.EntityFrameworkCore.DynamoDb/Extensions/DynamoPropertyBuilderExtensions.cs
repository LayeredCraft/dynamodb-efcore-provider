using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoPropertyBuilderExtensions type.</summary>
public static class DynamoPropertyBuilderExtensions
{
    extension(PropertyBuilder propertyBuilder)
    {
        /// <summary>Configures the DynamoDB attribute name used to store this scalar property.</summary>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public PropertyBuilder HasAttributeName(string name)
        {
            name.NotEmpty();
            propertyBuilder.Metadata.SetAttributeName(name);
            return propertyBuilder;
        }
    }

    extension<TProperty>(PropertyBuilder<TProperty> propertyBuilder)
    {
        /// <summary>Configures the DynamoDB attribute name used to store this scalar property.</summary>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public PropertyBuilder<TProperty> HasAttributeName(string name)
            => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder)
                .HasAttributeName(name);
    }

    extension(IConventionPropertyBuilder propertyBuilder)
    {
        /// <summary>
        ///     Configures the DynamoDB attribute name, respecting configuration source precedence.
        ///     Returns  if the name cannot be set due to a higher-priority
        ///     configuration already being present.
        /// </summary>
        /// <returns>The builder if the name was set; otherwise .</returns>
        public IConventionPropertyBuilder? HasAttributeName(
            string? name,
            bool fromDataAnnotation = false)
        {
            if (!propertyBuilder.CanSetAttributeName(name, fromDataAnnotation))
                return null;

            propertyBuilder.Metadata.SetAttributeName(name, fromDataAnnotation);
            return propertyBuilder;
        }

        /// <summary>
        ///     Returns a value indicating whether the DynamoDB attribute name can be set from the current
        ///     configuration source.
        /// </summary>
        public bool CanSetAttributeName(string? name, bool fromDataAnnotation = false)
            => propertyBuilder.CanSetAnnotation(
                DynamoAnnotationNames.AttributeName,
                name,
                fromDataAnnotation);
    }
}
