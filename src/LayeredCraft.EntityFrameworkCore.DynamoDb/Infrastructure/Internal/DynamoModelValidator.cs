using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

internal sealed class DynamoModelValidator(ModelValidatorDependencies dependencies)
    : ModelValidator(dependencies)
{
    /// <summary>Validates DynamoDB-specific model constraints after EF Core base validation.</summary>
    public override void Validate(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (!entityType.IsOwned())
                continue;

            ValidateOwnedEntityType(entityType);
        }

        ValidateEmbeddedOwnedCollectionNavigationShapes(model);
    }

    /// <summary>Validates primitive collection properties against DynamoDB provider shape constraints.</summary>
    protected override void ValidatePrimitiveCollections(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.ValidatePrimitiveCollections(model, logger);

        foreach (var entityType in model.GetEntityTypes())
        {
            ValidateTypeBase(entityType);

            foreach (var complexProperty in entityType.GetDeclaredComplexProperties())
                ValidateComplexType(complexProperty.ComplexType);
        }
    }

    /// <summary>Recursively validates primitive collection properties on a complex type graph.</summary>
    private static void ValidateComplexType(IComplexType complexType)
    {
        ValidateTypeBase(complexType);

        foreach (var complexProperty in complexType.GetDeclaredComplexProperties())
            ValidateComplexType(complexProperty.ComplexType);
    }

    /// <summary>Validates primitive collection properties declared directly on a type base.</summary>
    private static void ValidateTypeBase(ITypeBase typeBase)
    {
        foreach (var property in typeBase.GetDeclaredProperties())
        {
            if (!property.IsPrimitiveCollection)
                continue;

            if (DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(property.ClrType))
                continue;

            throw new InvalidOperationException(
                $"Property '{typeBase.DisplayName()}.{property.Name}' uses primitive collection CLR type "
                + $"'{property.ClrType.Name}', which is not supported by the DynamoDB provider. "
                + "Supported list shapes: T[], List<T>, IList<T>, IReadOnlyList<T>. "
                + "Supported set shapes: HashSet<T>, ISet<T>, IReadOnlySet<T>. "
                + "Supported dictionary shapes: Dictionary<string,TValue>, IDictionary<string,TValue>, "
                + "IReadOnlyDictionary<string,TValue>, ReadOnlyDictionary<string,TValue>. "
                + "Custom or derived concrete collection types are not supported.");
        }
    }

    /// <summary>Validates a single owned entity type for DynamoDB-specific mapping constraints.</summary>
    private static void ValidateOwnedEntityType(IEntityType entityType)
    {
        var tableName = GetTableName(entityType);
        if (!string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException(
                $"Owned entity type '{entityType.DisplayName()}' is configured with explicit table name "
                + $"'{tableName}'. Owned types must be embedded in their owner's DynamoDB "
                + "item and cannot have separate table mappings.");

        var ownership = entityType.FindOwnership();
        if (ownership?.PrincipalEntityType is not { } ownerEntityType)
            return;

        var containingAttributeName = entityType.GetContainingAttributeName();
        if (string.IsNullOrWhiteSpace(containingAttributeName))
            throw new InvalidOperationException(
                $"Owned entity type '{entityType.DisplayName()}' has an empty containing attribute name. "
                + "Containing attribute names must be non-empty.");

        ValidateContainingAttributeNameCollisions(
            entityType,
            ownerEntityType,
            containingAttributeName);

        if (!ownership.IsUnique)
            ValidateOwnedCollectionOrdinalKey(entityType);
    }

    /// <summary>Validates that an owned containing attribute name is unique within its owner entity type.</summary>
    private static void ValidateContainingAttributeNameCollisions(
        IEntityType entityType,
        IEntityType ownerEntityType,
        string containingAttributeName)
    {
        foreach (var property in ownerEntityType.GetDeclaredProperties())
        {
            if (!string.Equals(property.Name, containingAttributeName, StringComparison.Ordinal))
                continue;

            throw new InvalidOperationException(
                $"Owned entity type '{entityType.DisplayName()}' is configured with containing attribute name "
                + $"'{containingAttributeName}', which collides with scalar property "
                + $"'{ownerEntityType.DisplayName()}.{property.Name}'. Containing attribute names must be "
                + "unique.");
        }

        var currentOwnership = entityType.FindOwnership();

        foreach (var navigation in ownerEntityType.GetDeclaredNavigations())
        {
            if (!navigation.TargetEntityType.IsOwned())
                continue;

            if (navigation == currentOwnership?.PrincipalToDependent)
                continue;

            var otherContainingAttributeName =
                navigation.TargetEntityType.GetContainingAttributeName();
            if (!string.Equals(
                otherContainingAttributeName,
                containingAttributeName,
                StringComparison.Ordinal))
                continue;

            throw new InvalidOperationException(
                $"Owned entity type '{entityType.DisplayName()}' is configured with containing attribute name "
                + $"'{containingAttributeName}', which collides with owned navigation '{navigation.Name}' on "
                + $"'{ownerEntityType.DisplayName()}'. Containing attribute names must be unique.");
        }
    }

    /// <summary>Validates that owned collection element types include an ordinal key property.</summary>
    private static void ValidateOwnedCollectionOrdinalKey(IEntityType entityType)
    {
        var hasOrdinalKey = entityType
            .GetProperties()
            .Any(static property => property.IsOwnedOrdinalKeyProperty());

        if (hasOrdinalKey)
            return;

        throw new InvalidOperationException(
            $"Owned collection element entity type '{entityType.DisplayName()}' does not have an ordinal "
            + "key property. Owned collection elements require a synthetic ordinal key for stable "
            + "identity and change tracking.");
    }

    /// <summary>Validates embedded owned collection navigations use provider-supported list CLR shapes.</summary>
    private static void ValidateEmbeddedOwnedCollectionNavigationShapes(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var navigation in entityType.GetDeclaredNavigations())
            {
                if (!navigation.IsCollection
                    || !navigation.IsEmbedded()
                    || !navigation.TargetEntityType.IsOwned())
                    continue;

                if (DynamoTypeMappingSource.TryGetListElementType(navigation.ClrType, out _))
                    continue;

                throw new InvalidOperationException(
                    $"Embedded owned collection navigation '{entityType.DisplayName()}.{navigation.Name}' uses CLR type "
                    + $"'{navigation.ClrType.Name}', which is not supported by the DynamoDB provider. "
                    + "Supported list shapes: T[], List<T>, IList<T>, IReadOnlyList<T>.");
            }
        }
    }

    /// <summary>Gets the DynamoDB table name annotation configured for an entity type.</summary>
    private static string? GetTableName(IReadOnlyEntityType entityType)
        => entityType[DynamoAnnotationNames.TableName] as string;
}
