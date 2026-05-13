using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

/// <summary>Provides DynamoDB-specific fluent configuration for EF index builders.</summary>
public static class DynamoIndexBuilderExtensions
{
    extension(IndexBuilder indexBuilder)
    {
        /// <summary>Configures the DynamoDB secondary index name used for PartiQL index targeting.</summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder HasSecondaryIndexName(string? name)
        {
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            return indexBuilder;
        }

        /// <summary>Configures whether this EF index maps to a global or local DynamoDB secondary index.</summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder HasSecondaryIndexKind(DynamoSecondaryIndexKind? kind)
        {
            indexBuilder.Metadata.SetSecondaryIndexKind(kind);
            return indexBuilder;
        }

        /// <summary>Configures the DynamoDB projection type for this secondary index.</summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder HasSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType? projectionType)
        {
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(projectionType);
            return indexBuilder;
        }
    }

    extension<TEntity>(IndexBuilder<TEntity> indexBuilder) where TEntity : class
    {
        /// <summary>Configures the DynamoDB secondary index name used for PartiQL index targeting.</summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder<TEntity> HasSecondaryIndexName(string? name)
            => (IndexBuilder<TEntity>)((IndexBuilder)indexBuilder).HasSecondaryIndexName(name);

        /// <summary>Configures whether this EF index maps to a global or local DynamoDB secondary index.</summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder<TEntity> HasSecondaryIndexKind(DynamoSecondaryIndexKind? kind)
            => (IndexBuilder<TEntity>)((IndexBuilder)indexBuilder).HasSecondaryIndexKind(kind);

        /// <summary>Configures the DynamoDB projection type for this secondary index.</summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public IndexBuilder<TEntity> HasSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType? projectionType)
            => (IndexBuilder<TEntity>)((IndexBuilder)indexBuilder).HasSecondaryIndexProjectionType(
                projectionType);
    }

    extension(IConventionIndexBuilder indexBuilder)
    {
        /// <summary>Configures the DynamoDB secondary index name, respecting configuration source precedence.</summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>The same builder instance if the name was set; otherwise <see langword="null" />.</returns>
        public IConventionIndexBuilder? HasSecondaryIndexName(
            string? name,
            bool fromDataAnnotation = false)
        {
            name = name.NullButNotEmpty();
            if (!indexBuilder.CanSetSecondaryIndexName(name, fromDataAnnotation))
                return null;

            indexBuilder.Metadata.SetSecondaryIndexName(name, fromDataAnnotation);
            return indexBuilder;
        }

        /// <summary>
        ///     Returns whether the DynamoDB secondary index name can be set from the given configuration
        ///     source.
        /// </summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns><see langword="true" /> if the name can be set; otherwise <see langword="false" />.</returns>
        public bool CanSetSecondaryIndexName(string? name, bool fromDataAnnotation = false)
            => indexBuilder.CanSetAnnotation(
                DynamoAnnotationNames.SecondaryIndexName,
                name.NullButNotEmpty(),
                fromDataAnnotation);

        /// <summary>Configures whether this EF index maps to a global or local DynamoDB secondary index.</summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>The same builder instance if the kind was set; otherwise <see langword="null" />.</returns>
        public IConventionIndexBuilder? HasSecondaryIndexKind(
            DynamoSecondaryIndexKind? kind,
            bool fromDataAnnotation = false)
        {
            if (!indexBuilder.CanSetSecondaryIndexKind(kind, fromDataAnnotation))
                return null;

            indexBuilder.Metadata.SetSecondaryIndexKind(kind, fromDataAnnotation);
            return indexBuilder;
        }

        /// <summary>
        ///     Returns whether the DynamoDB secondary index kind can be set from the given configuration
        ///     source.
        /// </summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns><see langword="true" /> if the kind can be set; otherwise <see langword="false" />.</returns>
        public bool CanSetSecondaryIndexKind(
            DynamoSecondaryIndexKind? kind,
            bool fromDataAnnotation = false)
            => indexBuilder.CanSetAnnotation(
                DynamoAnnotationNames.SecondaryIndexKind,
                kind?.ToString(),
                fromDataAnnotation);

        /// <summary>Configures the DynamoDB projection type for this secondary index.</summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>
        ///     The same builder instance if the projection type was set; otherwise
        ///     <see langword="null" />.
        /// </returns>
        public IConventionIndexBuilder? HasSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType? projectionType,
            bool fromDataAnnotation = false)
        {
            if (!indexBuilder.CanSetSecondaryIndexProjectionType(
                projectionType,
                fromDataAnnotation))
                return null;

            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                projectionType,
                fromDataAnnotation);
            return indexBuilder;
        }

        /// <summary>
        ///     Returns whether the DynamoDB projection type can be set from the given configuration
        ///     source.
        /// </summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the projection type can be set; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public bool CanSetSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType? projectionType,
            bool fromDataAnnotation = false)
            => indexBuilder.CanSetAnnotation(
                DynamoAnnotationNames.SecondaryIndexProjectionType,
                projectionType?.ToString(),
                fromDataAnnotation);
    }
}
