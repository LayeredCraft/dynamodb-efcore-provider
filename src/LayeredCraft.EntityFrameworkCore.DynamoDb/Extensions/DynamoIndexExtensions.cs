// ReSharper disable CheckNamespace

using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Provides DynamoDB-specific metadata accessors for EF index metadata.</summary>
public static class DynamoIndexExtensions
{
    extension(IMutableIndex index)
    {
        /// <summary>Sets the DynamoDB secondary index name used for PartiQL index targeting.</summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        public void SetSecondaryIndexName(string? name)
            => index.SetOrRemoveAnnotation(
                DynamoAnnotationNames.SecondaryIndexName,
                name.NullButNotEmpty());

        /// <summary>Sets the DynamoDB secondary index kind for this EF index.</summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        public void SetSecondaryIndexKind(DynamoSecondaryIndexKind? kind)
            => index.SetOrRemoveAnnotation(
                DynamoAnnotationNames.SecondaryIndexKind,
                kind?.ToString());

        /// <summary>Sets the DynamoDB projection type for this EF index.</summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        public void SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType? projectionType)
            => index.SetOrRemoveAnnotation(
                DynamoAnnotationNames.SecondaryIndexProjectionType,
                projectionType?.ToString());
    }

    extension(IReadOnlyIndex index)
    {
        /// <summary>Gets the configured DynamoDB secondary index name.</summary>
        /// <returns>The configured name, or <see langword="null" /> when none has been configured.</returns>
        public string? GetSecondaryIndexName()
            => index[DynamoAnnotationNames.SecondaryIndexName] as string;

        /// <summary>Gets the configured DynamoDB secondary index kind.</summary>
        /// <returns>The configured kind, or <see langword="null" /> when none has been configured.</returns>
        public DynamoSecondaryIndexKind? GetSecondaryIndexKind()
            => Enum.TryParse<DynamoSecondaryIndexKind>(
                index[DynamoAnnotationNames.SecondaryIndexKind] as string,
                out var parsed)
                ? parsed
                : null;

        /// <summary>Gets the configured DynamoDB projection type for this EF index.</summary>
        /// <returns>
        ///     The configured projection type, or <see cref="DynamoSecondaryIndexProjectionType.All" />
        ///     when the index is configured as a DynamoDB secondary index and no explicit projection has
        ///     been stored.
        /// </returns>
        public DynamoSecondaryIndexProjectionType? GetSecondaryIndexProjectionType()
        {
            if (Enum.TryParse<DynamoSecondaryIndexProjectionType>(
                    index[DynamoAnnotationNames.SecondaryIndexProjectionType] as string,
                    out var parsed))
                return parsed;

            return index.GetSecondaryIndexKind() is not null
                ? DynamoSecondaryIndexProjectionType.All
                : null;
        }
    }

    extension(IConventionIndex index)
    {
        /// <summary>Sets the DynamoDB secondary index name at the given configuration source.</summary>
        /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation; otherwise the fluent API.
        /// </param>
        /// <returns>The applied name when successful; otherwise <see langword="null" />.</returns>
        public string? SetSecondaryIndexName(string? name, bool fromDataAnnotation = false)
            => (string?)index.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.SecondaryIndexName,
                    name.NullButNotEmpty(),
                    fromDataAnnotation)
                ?.Value;

        /// <summary>Sets the DynamoDB secondary index kind at the given configuration source.</summary>
        /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation; otherwise the fluent API.
        /// </param>
        /// <returns>The applied kind when successful; otherwise <see langword="null" />.</returns>
        public DynamoSecondaryIndexKind? SetSecondaryIndexKind(
            DynamoSecondaryIndexKind? kind,
            bool fromDataAnnotation = false)
            => Enum.TryParse<DynamoSecondaryIndexKind>(
                (string?)index.SetOrRemoveAnnotation(
                        DynamoAnnotationNames.SecondaryIndexKind,
                        kind?.ToString(),
                        fromDataAnnotation)
                    ?.Value,
                out var parsed)
                ? parsed
                : null;

        /// <summary>Sets the DynamoDB projection type at the given configuration source.</summary>
        /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation; otherwise the fluent API.
        /// </param>
        /// <returns>The applied projection type when successful; otherwise <see langword="null" />.</returns>
        public DynamoSecondaryIndexProjectionType? SetSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType? projectionType,
            bool fromDataAnnotation = false)
            => Enum.TryParse<DynamoSecondaryIndexProjectionType>(
                (string?)index.SetOrRemoveAnnotation(
                        DynamoAnnotationNames.SecondaryIndexProjectionType,
                        projectionType?.ToString(),
                        fromDataAnnotation)
                    ?.Value,
                out var parsed)
                ? parsed
                : null;

        /// <summary>Gets the configuration source for the DynamoDB secondary index name.</summary>
        /// <returns>
        ///     The <see cref="ConfigurationSource" /> of the annotation, or <see langword="null" /> if
        ///     none has been configured.
        /// </returns>
        public ConfigurationSource? GetSecondaryIndexNameConfigurationSource()
            => index.FindAnnotation(DynamoAnnotationNames.SecondaryIndexName)?.GetConfigurationSource();

        /// <summary>Gets the configuration source for the DynamoDB secondary index kind.</summary>
        /// <returns>
        ///     The <see cref="ConfigurationSource" /> of the annotation, or <see langword="null" /> if
        ///     none has been configured.
        /// </returns>
        public ConfigurationSource? GetSecondaryIndexKindConfigurationSource()
            => index.FindAnnotation(DynamoAnnotationNames.SecondaryIndexKind)?.GetConfigurationSource();

        /// <summary>Gets the configuration source for the DynamoDB projection type.</summary>
        /// <returns>
        ///     The <see cref="ConfigurationSource" /> of the annotation, or <see langword="null" /> if
        ///     none has been configured.
        /// </returns>
        public ConfigurationSource? GetSecondaryIndexProjectionTypeConfigurationSource()
            => index.FindAnnotation(DynamoAnnotationNames.SecondaryIndexProjectionType)
                ?.GetConfigurationSource();
    }
}
