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
        ValidateRootEntityHasPartitionKey(model);
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
    ///     Ensures each root entity type has a configured partition key annotation before EF base
    ///     validation runs.
    /// </summary>
    private static void ValidateRootEntityHasPartitionKey(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned()
                || entityType.FindOwnership() != null
                || entityType.BaseType != null)
                continue;

            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string)
                continue;

            throw new InvalidOperationException(
                $"No DynamoDB partition key is configured for entity type '{entityType.DisplayName()}'. "
                + "Use a conventional property name ('PK' or 'PartitionKey') or call "
                + "HasPartitionKey(...). The DynamoDB provider does not infer table keys from "
                + "EF Core's Id/[Key] conventions.");
        }
    }

    /// <summary>
    ///     Ensures that every root entity type with an explicit sort key annotation also has an
    ///     explicit partition key annotation.
    /// </summary>
    private static void ValidateSortKeyHasResolvablePartitionKey(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned()
                || entityType.FindOwnership() != null
                || entityType.BaseType != null)
                continue;

            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is not string skName)
                continue;

            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is not string)
                throw new InvalidOperationException(
                    $"Sort key property '{skName}' is configured on entity type "
                    + $"'{entityType.DisplayName()}' but no partition key can be determined. "
                    + "Call HasPartitionKey(...) or add a conventional partition key property name "
                    + "('PK' or 'PartitionKey').");
        }
    }

    /// <summary>
    ///     Validates that configured partition/sort key properties exist and that the EF primary key
    ///     matches exactly [PK] or [PK, SK].
    /// </summary>
    private static void ValidateKeyPropertyNames(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned()
                || entityType.FindOwnership() != null
                || entityType.BaseType != null)
                continue;

            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is not string pkName)
                continue;

            var pkProperty = entityType.FindProperty(pkName)
                ?? throw new InvalidOperationException(
                    $"The partition key property '{pkName}' configured on entity type "
                    + $"'{entityType.DisplayName()}' does not exist.");

            IProperty? skProperty = null;
            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is string skName)
            {
                skProperty = entityType.FindProperty(skName)
                    ?? throw new InvalidOperationException(
                        $"The sort key property '{skName}' configured on entity type "
                        + $"'{entityType.DisplayName()}' does not exist.");
            }

            var expectedPrimaryKey = new List<IProperty> { pkProperty };
            if (skProperty != null && skProperty != pkProperty)
                expectedPrimaryKey.Add(skProperty);

            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null)
                throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' must define an EF primary key that "
                    + "matches the DynamoDB table key.");

            if (primaryKey.Properties.SequenceEqual(expectedPrimaryKey))
                continue;

            var expectedDisplay = string.Join(", ", expectedPrimaryKey.Select(static p => p.Name));
            var actualDisplay = string.Join(", ", primaryKey.Properties.Select(static p => p.Name));
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' has EF primary key [{actualDisplay}] but "
                + $"the DynamoDB table key is [{expectedDisplay}]. Configure HasKey(...) to match "
                + "the DynamoDB partition/sort key order.");
        }
    }

    /// <summary>Validates all root entity types sharing the same DynamoDB table agree on key schema.</summary>
    private static void ValidateTableKeySchemaConsistency(IModel model)
    {
        var tableGroups = new Dictionary<string, List<IEntityType>>(StringComparer.Ordinal);
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned()
                || entityType.FindOwnership() != null
                || entityType.BaseType != null)
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
