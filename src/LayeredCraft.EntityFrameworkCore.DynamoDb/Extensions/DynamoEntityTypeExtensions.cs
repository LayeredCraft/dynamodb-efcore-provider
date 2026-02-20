// ReSharper disable CheckNamespace

using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoEntityTypeExtensions
{
    extension(IMutableEntityType entityType)
    {
        /// <summary>Sets the DynamoDB table name for the root entity type.</summary>
        /// <param name="name">The DynamoDB table name. Must be non-empty if provided.</param>
        public void SetTableName(string name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.TableName,
                name.NullButNotEmpty());

        /// <summary>Sets the containing top-level attribute name for an embedded owned entity type.</summary>
        /// <param name="name">
        ///     The top-level DynamoDB attribute name under which this owned entity is stored.
        ///     Pass <see langword="null" /> to revert to the navigation property name.
        /// </param>
        public void SetContainingAttributeName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.ContainingAttributeName,
                name.NullButNotEmpty());

        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB partition key.
        /// </summary>
        /// <param name="name">
        ///     The EF property name, or <see langword="null" /> to clear the explicit override.
        /// </param>
        public void SetPartitionKeyPropertyName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.PartitionKeyPropertyName,
                name.NullButNotEmpty());

        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB sort key.
        /// </summary>
        /// <param name="name">
        ///     The EF property name, or <see langword="null" /> to clear the explicit override.
        /// </param>
        public void SetSortKeyPropertyName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.SortKeyPropertyName,
                name.NullButNotEmpty());
    }

    extension(IReadOnlyEntityType entityType)
    {
        /// <summary>Gets the top-level DynamoDB attribute name used to store this owned entity type.</summary>
        /// <returns>
        ///     The explicitly configured attribute name, or the navigation property name as a fallback.
        ///     Returns <see langword="null" /> if no containing attribute name can be determined.
        /// </returns>
        public string? GetContainingAttributeName()
        {
            var configuredName =
                entityType[DynamoAnnotationNames.ContainingAttributeName] as string;
            if (!string.IsNullOrWhiteSpace(configuredName))
                return configuredName;

            return entityType.FindOwnership()?.PrincipalToDependent?.Name;
        }

        /// <summary>Gets the name of the EF property that maps to the DynamoDB partition key.</summary>
        /// <remarks>
        ///     Returns the configured partition key property annotation when present.
        /// </remarks>
        /// <returns>The EF property name, or <see langword="null" /> when no partition key is configured.</returns>
        public string? GetPartitionKeyPropertyName()
        {
            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string annotated)
                return annotated;
            return null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB partition key.</summary>
        /// <remarks>
        ///     Resolves the property from <see cref="GetPartitionKeyPropertyName()" />.
        /// </remarks>
        /// <returns>
        ///     The partition key <see cref="IReadOnlyProperty" />, or <see langword="null" /> when no
        ///     partition key is configured.
        /// </returns>
        public IReadOnlyProperty? GetPartitionKeyProperty()
        {
            var name = entityType.GetPartitionKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }

        /// <summary>Gets the name of the EF property that maps to the DynamoDB sort key.</summary>
        /// <remarks>
        ///     Returns the configured sort key property annotation when present.
        /// </remarks>
        /// <returns>The EF property name, or <see langword="null" /> if the table has no sort key.</returns>
        public string? GetSortKeyPropertyName()
        {
            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is string annotated)
                return annotated;
            return null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB sort key.</summary>
        /// <remarks>
        ///     Resolves the property from <see cref="GetSortKeyPropertyName()" />.
        /// </remarks>
        /// <returns>
        ///     The sort key <see cref="IReadOnlyProperty" />, or <see langword="null" /> if the table has
        ///     no sort key.
        /// </returns>
        public IReadOnlyProperty? GetSortKeyProperty()
        {
            var name = entityType.GetSortKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }
    }

    extension(IEntityType entityType)
    {
        /// <summary>Gets the EF property that maps to the DynamoDB partition key.</summary>
        /// <remarks>
        ///     Resolves the property from <see cref="GetPartitionKeyPropertyName()" />.
        /// </remarks>
        /// <returns>
        ///     The partition key <see cref="IProperty" />, or <see langword="null" /> when no partition key
        ///     is configured.
        /// </returns>
        public IProperty? GetPartitionKeyProperty()
        {
            var name = entityType.GetPartitionKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB sort key.</summary>
        /// <remarks>
        ///     Resolves the property from <see cref="GetSortKeyPropertyName()" />.
        /// </remarks>
        /// <returns>
        ///     The sort key <see cref="IProperty" />, or <see langword="null" /> if the table has no sort
        ///     key.
        /// </returns>
        public IProperty? GetSortKeyProperty()
        {
            var name = entityType.GetSortKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }
    }

    extension(IConventionEntityType entityType)
    {
        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB partition key at the given
        ///     configuration source.
        /// </summary>
        /// <param name="name">The EF property name, or <see langword="null" /> to clear the override.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>
        ///     The configured property name, or <see langword="null" /> if the configuration was not
        ///     applied.
        /// </returns>
        public string? SetPartitionKeyPropertyName(string? name, bool fromDataAnnotation = false)
            => (string?)entityType.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.PartitionKeyPropertyName,
                    name,
                    fromDataAnnotation)
                ?.Value;

        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB sort key at the given
        ///     configuration source.
        /// </summary>
        /// <param name="name">The EF property name, or <see langword="null" /> to clear the override.</param>
        /// <param name="fromDataAnnotation">
        ///     <see langword="true" /> if configured via a data annotation;
        ///     <see langword="false" /> for the fluent API.
        /// </param>
        /// <returns>
        ///     The configured property name, or <see langword="null" /> if the configuration was not
        ///     applied.
        /// </returns>
        public string? SetSortKeyPropertyName(string? name, bool fromDataAnnotation = false)
            => (string?)entityType.SetOrRemoveAnnotation(
                    DynamoAnnotationNames.SortKeyPropertyName,
                    name,
                    fromDataAnnotation)
                ?.Value;

        /// <summary>Returns the configuration source for the partition key property name annotation.</summary>
        /// <returns>
        ///     The <see cref="ConfigurationSource" /> of the annotation, or <see langword="null" /> if no
        ///     explicit partition key has been configured.
        /// </returns>
        public ConfigurationSource? GetPartitionKeyPropertyNameConfigurationSource()
            => entityType
                .FindAnnotation(DynamoAnnotationNames.PartitionKeyPropertyName)
                ?.GetConfigurationSource();

        /// <summary>Returns the configuration source for the sort key property name annotation.</summary>
        /// <returns>
        ///     The <see cref="ConfigurationSource" /> of the annotation, or <see langword="null" /> if no
        ///     explicit sort key has been configured.
        /// </returns>
        public ConfigurationSource? GetSortKeyPropertyNameConfigurationSource()
            => entityType
                .FindAnnotation(DynamoAnnotationNames.SortKeyPropertyName)
                ?.GetConfigurationSource();
    }
}
