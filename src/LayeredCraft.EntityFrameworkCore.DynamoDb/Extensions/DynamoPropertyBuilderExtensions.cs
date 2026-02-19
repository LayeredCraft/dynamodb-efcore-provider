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
}
