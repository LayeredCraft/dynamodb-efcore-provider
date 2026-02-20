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
        // Run DynamoDB-specific pre-flight checks before EF base validation so that
        // provider-specific error messages take precedence over EF's generic errors.
        ValidateSortKeyHasResolvablePartitionKey(model);

        base.Validate(model, logger);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (!entityType.IsOwned())
                continue;

            ValidateOwnedEntityType(entityType);
        }

        ValidateEmbeddedOwnedCollectionNavigationShapes(model);
        ValidateKeyPropertyNames(model);
        ValidateTableKeySchemaConsistency(model);
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

    /// <summary>
    ///     Ensures that every root entity type with an explicit sort key annotation has a
    ///     determinable partition key — either via <c>HasPartitionKey</c> or auto-discovery from the EF
    ///     primary key. This runs before EF base validation so that a DynamoDB-specific error is emitted
    ///     instead of EF's generic "no primary key defined" message.
    /// </summary>
    private static void ValidateSortKeyHasResolvablePartitionKey(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.FindOwnership() != null)
                continue;

            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is not string skName)
                continue;

            if (entityType.GetPartitionKeyPropertyName() is null)
                throw new InvalidOperationException(
                    $"Sort key property '{skName}' is configured on entity type "
                    + $"'{entityType.DisplayName()}' but no partition key can be determined. "
                    + "Call HasPartitionKey(...) to designate a partition key property, or ensure "
                    + "the entity type has a primary key property discoverable by EF Core "
                    + "(for example, a property named 'Id' or decorated with [Key]).");
        }
    }

    /// <summary>
    ///     Validates that any explicitly configured partition/sort key properties exist on the entity
    ///     type and are members of its EF primary key, ensuring EF identity and DynamoDB identity stay
    ///     aligned.
    /// </summary>
    private static void ValidateKeyPropertyNames(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.FindOwnership() != null)
                continue;

            var primaryKey = entityType.FindPrimaryKey();
            var primaryKeyPropertyNames = primaryKey?.Properties.Select(p => p.Name).ToHashSet()
                ?? [];

            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string pkName)
            {
                if (entityType.FindProperty(pkName) == null)
                    throw new InvalidOperationException(
                        $"The partition key property '{pkName}' configured on entity type "
                        + $"'{entityType.DisplayName()}' does not exist.");

                if (!primaryKeyPropertyNames.Contains(pkName))
                    throw new InvalidOperationException(
                        $"The partition key property '{pkName}' on entity type "
                        + $"'{entityType.DisplayName()}' is not part of the EF primary key. "
                        + "DynamoDB key properties must be declared as EF primary key members "
                        + "so that change tracking can correctly identify entities.");
            }

            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is string skName)
            {
                if (entityType.FindProperty(skName) == null)
                    throw new InvalidOperationException(
                        $"The sort key property '{skName}' configured on entity type "
                        + $"'{entityType.DisplayName()}' does not exist.");

                if (!primaryKeyPropertyNames.Contains(skName))
                    throw new InvalidOperationException(
                        $"The sort key property '{skName}' on entity type "
                        + $"'{entityType.DisplayName()}' is not part of the EF primary key. "
                        + "DynamoDB key properties must be declared as EF primary key members "
                        + "so that change tracking can correctly identify entities.");
            }
        }
    }

    /// <summary>Validates all root entity types sharing the same DynamoDB table agree on key schema.</summary>
    private static void ValidateTableKeySchemaConsistency(IModel model)
    {
        var tableGroups = new Dictionary<string, List<IEntityType>>(StringComparer.Ordinal);
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.FindOwnership() != null)
                continue;
            var tableName =
                entityType[DynamoAnnotationNames.TableName] as string ?? entityType.ClrType.Name;
            if (!tableGroups.TryGetValue(tableName, out var group))
                tableGroups[tableName] = group = [];
            group.Add(entityType);
        }

        foreach (var (tableName, entityTypes) in tableGroups)
        {
            if (entityTypes.Count <= 1)
                continue;
            ValidateTableKeySchemaGroup(tableName, entityTypes);
        }
    }

    /// <summary>Validates PK/SK name consistency within a single table group.</summary>
    private static void ValidateTableKeySchemaGroup(string tableName, List<IEntityType> entityTypes)
    {
        var first = entityTypes[0];
        var expectedPk = first.GetPartitionKeyProperty()?.GetAttributeName();
        var expectedSk = first.GetSortKeyProperty()?.GetAttributeName();
        for (var i = 1; i < entityTypes.Count; i++)
        {
            var et = entityTypes[i];
            var pk = et.GetPartitionKeyProperty()?.GetAttributeName();
            var sk = et.GetSortKeyProperty()?.GetAttributeName();
            if (!string.Equals(pk, expectedPk, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' but have different partition key attribute names ('{expectedPk ?? "<none>"}' vs '{pk ?? "<none>"}'). "
                    + "All entity types sharing a DynamoDB table must use the same partition key attribute name.");
            if (!string.Equals(sk, expectedSk, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' but have inconsistent sort key attribute names "
                    + $"('{expectedSk ?? "<none>"}' vs '{sk ?? "<none>"}')."
                    + " All entity types sharing a DynamoDB table must agree on the sort key attribute name.");
        }
    }

    /// <summary>Gets the DynamoDB table name annotation configured for an entity type.</summary>
    private static string? GetTableName(IReadOnlyEntityType entityType)
        => entityType[DynamoAnnotationNames.TableName] as string;
}
