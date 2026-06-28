using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Configures default discriminator metadata for DynamoDB entity mappings.</summary>
/// <remarks>
///     A discriminator is conventionally configured for every entity type unless explicitly disabled.
///     The discriminator property name comes from <c>IReadOnlyModel.GetEmbeddedDiscriminatorName</c>,
///     which defaults to <c>$type</c>.
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
        => ProcessModel(entityTypeBuilder.Metadata.Model);

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

        ProcessModel(entityTypeBuilder.Metadata.Model);
    }

    /// <inheritdoc />
    public override void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        base.ProcessEntityTypeBaseTypeChanged(entityTypeBuilder, newBaseType, oldBaseType, context);

        ProcessModel(entityTypeBuilder.Metadata.Model);
    }

    /// <inheritdoc />
    public override void ProcessEntityTypeRemoved(
        IConventionModelBuilder modelBuilder,
        IConventionEntityType entityType,
        IConventionContext<IConventionEntityType> context)
    {
        base.ProcessEntityTypeRemoved(modelBuilder, entityType, context);

        ProcessModel(modelBuilder.Metadata);
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
            ProcessModel(entityType.Model);
            return;
        }

        if (entityType.GetDiscriminatorPropertyConfigurationSource()
            == ConfigurationSource.Convention)
            return;

        SetDiscriminatorDisabledAnnotation(entityType, true);
        ProcessModel(entityType.Model);
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

        ProcessModel(modelBuilder.Metadata);
    }

    /// <summary>Applies discriminator conventions to entity table groups.</summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
        => ProcessModel(modelBuilder.Metadata);

    private void ProcessModel(IConventionModel model)
    {
        var rootEntityTypes = model
            .GetEntityTypes()
            .Where(static entityType => entityType.BaseType is null)
            .ToList();

        foreach (var tableGroup in rootEntityTypes.GroupBy(static entityType
            => entityType.ComputeTableGroupName()))
        {
            if (tableGroup.Any(static rootEntityType
                => IsDiscriminatorDisabled(rootEntityType)
                || HasExplicitNoDiscriminator(rootEntityType)))
            {
                foreach (var rootEntityType in tableGroup)
                {
                    rootEntityType.Builder.HasNoDiscriminator();
                    SetDiscriminatorDisabledAnnotation(rootEntityType, true);
                }

                continue;
            }

            foreach (var rootEntityType in tableGroup)
            {
                SetDiscriminatorDisabledAnnotation(rootEntityType, false);

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

    private static void SetDiscriminatorDisabledAnnotation(
        IConventionEntityType entityType,
        bool disabled)
        => entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.DiscriminatorDisabled,
            disabled ? true : null);

    private static bool IsDiscriminatorDisabled(IConventionEntityType entityType)
        => entityType[DynamoAnnotationNames.DiscriminatorDisabled] as bool? == true;

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
