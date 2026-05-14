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

        if (structuralTypeBuilder.Metadata is not IConventionEntityType entityType)
            return;

        if (name is not null)
        {
            SetDiscriminatorDisabledAnnotation(entityType, false);
            ProcessModel(entityType.Model, false);
            return;
        }

        if (entityType.GetDiscriminatorPropertyConfigurationSource()
            == ConfigurationSource.Convention)
            return;

        SetDiscriminatorDisabledAnnotation(entityType, true);
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

    private void ProcessModel(IConventionModel model, bool finalizing)
    {
        var rootEntityTypes = model
            .GetEntityTypes()
            .Where(static entityType => entityType.BaseType is null)
            .ToList();

        foreach (var tableGroup in rootEntityTypes.GroupBy(static entityType
            => entityType.ComputeTableGroupName()))
        {
            var hasHierarchy =
                tableGroup.Any(static entityType => entityType.GetDerivedTypes().Any());

            var concreteEntityTypes = tableGroup
                .SelectMany(static entityType => entityType.GetConcreteDerivedTypesInclusive())
                .Distinct()
                .ToList();

            if (concreteEntityTypes.Count <= 1 && !hasHierarchy)
            {
                foreach (var rootEntityType in tableGroup)
                {
                    if (IsDiscriminatorDisabled(rootEntityType)
                        || HasExplicitNoDiscriminator(rootEntityType))
                    {
                        RemoveConventionDiscriminator(rootEntityType);
                        SetDiscriminatorDisabledAnnotation(rootEntityType, true);
                        continue;
                    }

                    if (finalizing)
                    {
                        RemoveConventionDiscriminator(rootEntityType);
                        continue;
                    }

                    ConfigureDiscriminator(rootEntityType);
                }

                continue;
            }

            foreach (var rootEntityType in tableGroup)
            {
                if (IsDiscriminatorDisabled(rootEntityType)
                    || HasExplicitNoDiscriminator(rootEntityType))
                {
                    RemoveConventionDiscriminator(rootEntityType);
                    SetDiscriminatorDisabledAnnotation(rootEntityType, true);
                    continue;
                }

                SetDiscriminatorDisabledAnnotation(rootEntityType, false);
                ConfigureDiscriminator(rootEntityType);
            }
        }
    }

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

    /// <summary>Configures discriminator metadata for an entity type that should be discriminated.</summary>
    private void ConfigureDiscriminator(IConventionEntityType rootEntityType)
    {
        var discriminatorProperty = rootEntityType.FindDiscriminatorProperty();
        var preserveExistingDiscriminator = discriminatorProperty is not null
            && rootEntityType.GetDiscriminatorPropertyConfigurationSource()
            != ConfigurationSource.Convention;

        var discriminatorBuilder = preserveExistingDiscriminator
            ? rootEntityType.Builder.HasDiscriminator(
                discriminatorProperty!.Name,
                discriminatorProperty.ClrType)
            : rootEntityType.Builder.HasDiscriminator(
                rootEntityType.Model.GetEmbeddedDiscriminatorName(),
                typeof(string));

        if (discriminatorBuilder is null)
            return;

        SetDefaultDiscriminatorValues(
            rootEntityType.GetDerivedTypesInclusive(),
            discriminatorBuilder);
    }

    /// <summary>Stores or clears the provider annotation that preserves explicit discriminator opt-out.</summary>
    private static void SetDiscriminatorDisabledAnnotation(
        IConventionEntityType entityType,
        bool disabled)
        => entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.DiscriminatorDisabled,
            disabled ? true : null);

    /// <summary>Returns whether the entity type has explicitly opted out of discriminator metadata.</summary>
    private static bool IsDiscriminatorDisabled(IConventionEntityType entityType)
        => entityType[DynamoAnnotationNames.DiscriminatorDisabled] as bool? == true;

    /// <summary>Returns whether EF Core metadata records an explicit discriminator removal.</summary>
    private static bool HasExplicitNoDiscriminator(IConventionEntityType entityType)
        => entityType.FindDiscriminatorProperty() is null
            && entityType.GetDiscriminatorPropertyConfigurationSource() is { } source
            && source != ConfigurationSource.Convention;

    /// <summary>Sets default discriminator values to each entity type's short name.</summary>
    protected override void SetDefaultDiscriminatorValues(
        IEnumerable<IConventionEntityType> entityTypes,
        IConventionDiscriminatorBuilder discriminatorBuilder)
    {
        foreach (var entityType in entityTypes)
            discriminatorBuilder.HasValue(entityType, entityType.ShortName());
    }
}
