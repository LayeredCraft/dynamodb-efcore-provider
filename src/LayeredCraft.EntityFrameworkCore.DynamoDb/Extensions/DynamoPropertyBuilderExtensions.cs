using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

public static class DynamoPropertyBuilderExtensions
{
    extension(PropertyBuilder propertyBuilder)
    {
        /// <summary>Configures the DynamoDB attribute name used to store this scalar property.</summary>
        /// <param name="propertyBuilder">The builder for the property being configured.</param>
        /// <param name="name">The DynamoDB attribute name.</param>
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
        /// <typeparam name="TProperty">The property CLR type.</typeparam>
        /// <param name="propertyBuilder">The builder for the property being configured.</param>
        /// <param name="name">The DynamoDB attribute name.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public PropertyBuilder<TProperty> HasAttributeName(string name)
            => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder)
                .HasAttributeName(name);
    }

    extension(IConventionPropertyBuilder propertyBuilder)
    {
        /// <summary>
        ///     Configures the DynamoDB attribute name, respecting configuration source precedence.
        ///     Returns <see langword="null" /> if the name cannot be set due to a higher-priority
        ///     configuration already being present.
        /// </summary>
        /// <param name="propertyBuilder">The convention builder for the property being configured.</param>
        /// <param name="name">The DynamoDB attribute name, or <see langword="null" /> to reset to default.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        /// <returns>The builder if the name was set; otherwise <see langword="null" />.</returns>
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
        /// <param name="propertyBuilder">The convention builder for the property being configured.</param>
        /// <param name="name">The DynamoDB attribute name to check.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        public bool CanSetAttributeName(string? name, bool fromDataAnnotation = false)
            => propertyBuilder.CanSetAnnotation(
                DynamoAnnotationNames.AttributeName,
                name,
                fromDataAnnotation);
    }
}
