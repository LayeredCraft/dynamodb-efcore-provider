using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

public static class DynamoEntityTypeBuilderExtensions
{
    extension(EntityTypeBuilder entityTypeBuilder)
    {
        /// <summary>
        ///     Configures the table that the entity type maps to when targeting AWS DynamoDB.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="name">The name of the table.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder ToTable(string? name)
        {
            Check.NullButNotEmpty(name);

            entityTypeBuilder.Metadata.SetTableName(name!);

            return entityTypeBuilder;
        }
    }

    extension<TEntity>(EntityTypeBuilder<TEntity> entityTypeBuilder) where TEntity : class
    {
        /// <summary>
        ///     Configures the table that the entity type maps to when targeting AWS DynamoDB.
        /// </summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="name">The name of the container.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> ToTable(string? name)
            => (EntityTypeBuilder<TEntity>)((EntityTypeBuilder)entityTypeBuilder).ToTable(name);
    }
}
