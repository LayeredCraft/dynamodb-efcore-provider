// ReSharper disable CheckNamespace

using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoEntityTypeExtensions type.</summary>
public static class DynamoEntityTypeExtensions
{
    extension(IMutableEntityType entityType)
    {
        /// <summary>Sets the DynamoDB table name for the root entity type.</summary>
        ///     The DynamoDB table name. Pass  to clear the explicit mapping.
        public void SetTableName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.TableName,
                name.NullButNotEmpty());

        /// <summary>Sets the containing top-level attribute name for an embedded owned entity type.</summary>
        ///     The top-level DynamoDB attribute name under which this owned entity is stored.
        ///     Pass  to revert to the navigation property name.
        public void SetContainingAttributeName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.ContainingAttributeName,
                name.NullButNotEmpty());

        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB partition key.
        /// </summary>
        ///     The EF property name, or  to clear the explicit override.
        public void SetPartitionKeyPropertyName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.PartitionKeyPropertyName,
                name.NullButNotEmpty());

        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB sort key.
        /// </summary>
        ///     The EF property name, or  to clear the explicit override.
        public void SetSortKeyPropertyName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.SortKeyPropertyName,
                name.NullButNotEmpty());

        /// <summary>Sets the discriminator strategy used for DynamoDB shared-table mappings.</summary>
        ///     The strategy to apply, or  to clear the explicit
        ///     setting.
        public void SetDiscriminatorStrategy(DynamoDiscriminatorStrategy? strategy)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.DiscriminatorStrategy,
                strategy?.ToString());
    }

    extension(IReadOnlyEntityType entityType)
    {
        /// <summary>Gets the top-level DynamoDB attribute name used to store this owned entity type.</summary>
        /// <returns>
        ///     The explicitly configured attribute name, or the navigation property name as a fallback.
        ///     Returns  if no containing attribute name can be determined.
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
        /// <returns>The EF property name, or  when no partition key is configured.</returns>
        public string? GetPartitionKeyPropertyName()
        {
            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string annotated)
                return annotated;
            return null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB partition key.</summary>
        /// <remarks>
        ///     Resolves the property from <c>GetPartitionKeyPropertyName()</c>.
        /// </remarks>
        /// <returns>
        ///     The partition key <c>IReadOnlyProperty</c>, or  when no
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
        /// <returns>The EF property name, or  if the table has no sort key.</returns>
        public string? GetSortKeyPropertyName()
        {
            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is string annotated)
                return annotated;
            return null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB sort key.</summary>
        /// <remarks>
        ///     Resolves the property from <c>GetSortKeyPropertyName()</c>.
        /// </remarks>
        /// <returns>
        ///     The sort key <c>IReadOnlyProperty</c>, or  if the table has
        ///     no sort key.
        /// </returns>
        public IReadOnlyProperty? GetSortKeyProperty()
        {
            var name = entityType.GetSortKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }

        /// <summary>Gets the configured discriminator strategy for DynamoDB shared-table mappings.</summary>
        /// <returns>
        ///     The configured strategy when present; otherwise
        ///     <c>DynamoDiscriminatorStrategy.Attribute</c>.
        /// </returns>
        public DynamoDiscriminatorStrategy GetDiscriminatorStrategy()
        {
            if (entityType[DynamoAnnotationNames.DiscriminatorStrategy] is string strategyName
                && Enum.TryParse<DynamoDiscriminatorStrategy>(strategyName, out var strategy))
                return strategy;

            return DynamoDiscriminatorStrategy.Attribute;
        }
    }

    extension(IEntityType entityType)
    {
        /// <summary>Gets the EF property that maps to the DynamoDB partition key.</summary>
        /// <remarks>
        ///     Resolves the property from <c>GetPartitionKeyPropertyName()</c>.
        /// </remarks>
        /// <returns>
        ///     The partition key <c>IProperty</c>, or  when no partition key
        ///     is configured.
        /// </returns>
        public IProperty? GetPartitionKeyProperty()
        {
            var name = entityType.GetPartitionKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }

        /// <summary>Gets the EF property that maps to the DynamoDB sort key.</summary>
        /// <remarks>
        ///     Resolves the property from <c>GetSortKeyPropertyName()</c>.
        /// </remarks>
        /// <returns>
        ///     The sort key <c>IProperty</c>, or  if the table has no sort
        ///     key.
        /// </returns>
        public IProperty? GetSortKeyProperty()
        {
            var name = entityType.GetSortKeyPropertyName();
            return name is not null ? entityType.FindProperty(name) : null;
        }

        /// <summary>Gets the configured discriminator strategy for DynamoDB shared-table mappings.</summary>
        /// <returns>
        ///     The configured strategy when present; otherwise
        ///     <c>DynamoDiscriminatorStrategy.Attribute</c>.
        /// </returns>
        public DynamoDiscriminatorStrategy GetDiscriminatorStrategy()
            => ((IReadOnlyEntityType)entityType).GetDiscriminatorStrategy();
    }

    extension(IConventionEntityType entityType)
    {
        /// <summary>
        ///     Sets the EF property name designated as the DynamoDB partition key at the given
        ///     configuration source.
        /// </summary>
        ///      if configured via a data annotation;
        ///      for the fluent API.
        /// <returns>
        ///     The configured property name, or  if the configuration was not
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
        ///      if configured via a data annotation;
        ///      for the fluent API.
        /// <returns>
        ///     The configured property name, or  if the configuration was not
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
        ///     The <c>ConfigurationSource</c> of the annotation, or  if no
        ///     explicit partition key has been configured.
        /// </returns>
        public ConfigurationSource? GetPartitionKeyPropertyNameConfigurationSource()
            => entityType
                .FindAnnotation(DynamoAnnotationNames.PartitionKeyPropertyName)
                ?.GetConfigurationSource();

        /// <summary>Returns the configuration source for the sort key property name annotation.</summary>
        /// <returns>
        ///     The <c>ConfigurationSource</c> of the annotation, or  if no
        ///     explicit sort key has been configured.
        /// </returns>
        public ConfigurationSource? GetSortKeyPropertyNameConfigurationSource()
            => entityType
                .FindAnnotation(DynamoAnnotationNames.SortKeyPropertyName)
                ?.GetConfigurationSource();

        /// <summary>
        ///     Sets the discriminator strategy used for DynamoDB shared-table mappings at the given
        ///     configuration source.
        /// </summary>
        ///     The strategy to set, or  to clear the explicit
        ///     setting.
        ///      if configured via a data annotation;
        ///      for the fluent API.
        /// <returns>The applied strategy when configuration succeeded; otherwise .</returns>
        public DynamoDiscriminatorStrategy? SetDiscriminatorStrategy(
            DynamoDiscriminatorStrategy? strategy,
            bool fromDataAnnotation = false)
            => Enum.TryParse<DynamoDiscriminatorStrategy>(
                (string?)entityType.SetOrRemoveAnnotation(
                        DynamoAnnotationNames.DiscriminatorStrategy,
                        strategy?.ToString(),
                        fromDataAnnotation)
                    ?.Value,
                out var parsed)
                ? parsed
                : null;

        /// <summary>Returns the configuration source for the discriminator strategy annotation.</summary>
        /// <returns>
        ///     The <c>ConfigurationSource</c> of the annotation, or  if no
        ///     explicit strategy has been configured.
        /// </returns>
        public ConfigurationSource? GetDiscriminatorStrategyConfigurationSource()
            => entityType
                .FindAnnotation(DynamoAnnotationNames.DiscriminatorStrategy)
                ?.GetConfigurationSource();

        /// <summary>Returns whether the discriminator strategy can be set from the given configuration source.</summary>
        ///      if configured via a data annotation;
        ///      for the fluent API.
        /// <returns>
        ///      when the new value can be applied; otherwise
        ///     .
        /// </returns>
        public bool CanSetDiscriminatorStrategy(
            DynamoDiscriminatorStrategy? strategy,
            bool fromDataAnnotation = false)
            => fromDataAnnotation
                ? ConfigurationSource.DataAnnotation.Overrides(
                    entityType.GetDiscriminatorStrategyConfigurationSource())
                || entityType.GetDiscriminatorStrategy() == strategy
                : ConfigurationSource.Convention.Overrides(
                    entityType.GetDiscriminatorStrategyConfigurationSource())
                || entityType.GetDiscriminatorStrategy() == strategy;
    }
}
