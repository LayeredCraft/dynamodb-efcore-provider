using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Configures default discriminator metadata for shared DynamoDB table mappings.</summary>
/// <remarks>
///     A discriminator is conventionally configured when multiple concrete entity types are
///     mapped to the same table group. The discriminator property name comes from
///     <c>IReadOnlyModel.GetEmbeddedDiscriminatorName</c>, which defaults to <c>$type</c>.
/// </remarks>
public sealed class DynamoDiscriminatorConvention(
    ProviderConventionSetBuilderDependencies dependencies) : DiscriminatorConvention(dependencies),
    IEntityTypeAddedConvention,
    IEntityTypeAnnotationChangedConvention,
    IModelEmbeddedDiscriminatorNameConvention,
    IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
        => ProcessModel(entityTypeBuilder.Metadata.Model, false);

    /// <inheritdoc />
    public void ProcessEntityTypeAnnotationChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        string name,
        IConventionAnnotation? annotation,
        IConventionAnnotation? oldAnnotation,
        IConventionContext<IConventionAnnotation> context)
    {
        if (name != DynamoAnnotationNames.TableName)
            return;

        var newTableName = annotation?.Value as string;
        var oldTableName = oldAnnotation?.Value as string;
        if (string.Equals(newTableName, oldTableName, StringComparison.Ordinal))
            return;

        ProcessModel(entityTypeBuilder.Metadata.Model, false);
    }

    /// <inheritdoc />
    public override void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        base.ProcessEntityTypeBaseTypeChanged(entityTypeBuilder, newBaseType, oldBaseType, context);

        ProcessModel(entityTypeBuilder.Metadata.Model, false);
    }

    /// <inheritdoc />
    public override void ProcessEntityTypeRemoved(
        IConventionModelBuilder modelBuilder,
        IConventionEntityType entityType,
        IConventionContext<IConventionEntityType> context)
    {
        base.ProcessEntityTypeRemoved(modelBuilder, entityType, context);

        ProcessModel(modelBuilder.Metadata, false);
    }

    /// <inheritdoc />
    public override void ProcessDiscriminatorPropertySet(
        IConventionTypeBaseBuilder structuralTypeBuilder,
        string? name,
        IConventionContext<string> context)
    {
        base.ProcessDiscriminatorPropertySet(structuralTypeBuilder, name, context);

        if (name is not null && structuralTypeBuilder.Metadata is IConventionEntityType entityType)
            ProcessModel(entityType.Model, false);
    }

    /// <inheritdoc />
    public void ProcessEmbeddedDiscriminatorName(
        IConventionModelBuilder modelBuilder,
        string? newName,
        string? oldName,
        IConventionContext<string> context)
    {
        if (string.Equals(newName, oldName, StringComparison.Ordinal))
            return;

        ProcessModel(modelBuilder.Metadata, false);
    }

    /// <summary>Applies discriminator conventions to table groups that map multiple concrete entity types.</summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
        => ProcessModel(modelBuilder.Metadata, true);

    private void ProcessModel(IConventionModel model, bool removeSingleTypeConventionDiscriminators)
    {
        var rootEntityTypes = model
                .GetEntityTypes()
                .Where(static entityType => !entityType.IsOwned()
                    && entityType.FindOwnership() is null
                    && entityType.BaseType is null)
                .ToList();

        foreach (var tableGroup in rootEntityTypes.GroupBy(static entityType
            => entityType.GetTableGroupName()))
        {
            var hasHierarchy =
                tableGroup.Any(static entityType => entityType.GetDerivedTypes().Any());

            var concreteEntityTypes = tableGroup
                .SelectMany(static entityType => entityType.GetConcreteDerivedTypesInclusive())
                .Where(static entityType => !entityType.IsOwned())
                .Distinct()
                .ToList();

            if (concreteEntityTypes.Count <= 1 && !hasHierarchy)
            {
                if (!removeSingleTypeConventionDiscriminators)
                {
                    if (tableGroup.Any(HasExplicitNoDiscriminator))
                        continue;

                    foreach (var rootEntityType in tableGroup)
                    {
                        var discriminatorBuilder = rootEntityType.Builder.HasDiscriminator(
                            rootEntityType.Model.GetEmbeddedDiscriminatorName(),
                            typeof(string));

                        if (discriminatorBuilder is null)
                            continue;

                        SetDefaultDiscriminatorValues(
                            rootEntityType.GetDerivedTypesInclusive(),
                            discriminatorBuilder);
                    }

                    continue;
                }

                foreach (var rootEntityType in tableGroup)
                    RemoveConventionDiscriminator(rootEntityType);
                continue;
            }

            if (tableGroup.Any(HasExplicitNoDiscriminator))
            {
                foreach (var rootEntityType in tableGroup)
                    rootEntityType.Builder.HasNoDiscriminator();

                continue;
            }

            foreach (var rootEntityType in tableGroup)
            {
                var discriminatorBuilder = rootEntityType.Builder.HasDiscriminator(
                    rootEntityType.Model.GetEmbeddedDiscriminatorName(),
                    typeof(string));

                if (discriminatorBuilder is null)
                    continue;

                SetDefaultDiscriminatorValues(
                    rootEntityType.GetDerivedTypesInclusive(),
                    discriminatorBuilder);
            }
        }
    }

    /// <summary>Checks whether discriminator metadata was explicitly removed for the root entity type.</summary>
    private static bool HasExplicitNoDiscriminator(IConventionEntityType entityType)
        => entityType.FindDiscriminatorProperty() is null
            && entityType.GetDiscriminatorPropertyConfigurationSource() is { } source
            && source != ConfigurationSource.Convention;

    /// <summary>Removes convention-added discriminator metadata while preserving explicit configuration.</summary>
    private static void RemoveConventionDiscriminator(IConventionEntityType entityType)
    {
        if (entityType.FindDiscriminatorProperty() is null)
            return;

        if (entityType.GetDiscriminatorPropertyConfigurationSource()
            != ConfigurationSource.Convention)
            return;

        entityType.Builder.HasNoDiscriminator();
    }

    /// <summary>Sets default discriminator values to each entity type's short name.</summary>
    protected override void SetDefaultDiscriminatorValues(
        IEnumerable<IConventionEntityType> entityTypes,
        IConventionDiscriminatorBuilder discriminatorBuilder)
    {
        foreach (var entityType in entityTypes)
            discriminatorBuilder.HasValue(entityType, entityType.ShortName());
    }
}
