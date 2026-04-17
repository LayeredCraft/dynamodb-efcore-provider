using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

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
        ValidateRootEntityDoesNotUseExplicitPrimaryKeyConfiguration(model);
        ValidateRootEntityHasPartitionKey(model);
        ValidateSortKeyHasResolvablePartitionKey(model);
        ValidateConfiguredKeyPropertiesExist(model);

        base.Validate(model, logger);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (!entityType.IsOwned())
                continue;

            ValidateOwnedEntityType(entityType);
        }

        ValidateEmbeddedOwnedCollectionNavigationShapes(model);
        ValidateKeyPropertyNames(model);
        ValidateKeyPropertyTypes(model);
        ValidateKeyPropertyNullability(model);
        ValidateTableKeySchemaConsistency(model);
        ValidateSecondaryIndexes(model);
        ValidateDiscriminatorMappings(model);
        ValidateConcurrencyTokenConfiguration(model);
        ValidateScalarPropertyTypeMappings(model, logger);
    }

    /// <summary>Rejects explicit EF primary key configuration for root DynamoDB entities.</summary>
    private static void ValidateRootEntityDoesNotUseExplicitPrimaryKeyConfiguration(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
                continue;

            if (primaryKey is not IConventionKey conventionKey
                || conventionKey.GetConfigurationSource() is not (ConfigurationSource.Explicit
                    or ConfigurationSource.DataAnnotation))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' configures an EF primary key explicitly. "
                + "Root DynamoDB entities must use HasPartitionKey(...) and optional HasSortKey(...); "
                + "do not use HasKey(...) or [Key]. The EF primary key is derived automatically from "
                + "the DynamoDB key schema.");
        }
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
            var attributeName = property.GetAttributeName();
            if (!string.Equals(attributeName, containingAttributeName, StringComparison.Ordinal))
                continue;

            throw new InvalidOperationException(
                $"Owned entity type '{entityType.DisplayName()}' is configured with containing attribute name "
                + $"'{containingAttributeName}', which collides with scalar property "
                + $"'{ownerEntityType.DisplayName()}.{property.Name}' mapped to attribute '{attributeName}'. "
                + "Containing attribute names must be "
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
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is string)
                continue;

            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey?.Properties.Count > 2)
                throw new InvalidOperationException(
                    $"No DynamoDB partition key is configured for entity type '{entityType.DisplayName()}'. "
                    + $"The EF primary key has {primaryKey.Properties.Count} properties, but the DynamoDB "
                    + "provider supports only one- or two-part keys (partition key with optional sort key). "
                    + "Configure HasPartitionKey(...) and HasSortKey(...) or use conventional property "
                    + "names ('PK'/'PartitionKey' and 'SK'/'SortKey') to map the DynamoDB table key explicitly.");

            throw new InvalidOperationException(
                $"No DynamoDB partition key is configured for entity type '{entityType.DisplayName()}'. "
                + "Use a conventional property name ('PK' or 'PartitionKey') or call "
                + "HasPartitionKey(...). The DynamoDB provider does not infer table keys from "
                + "HasKey(...), Id, or [Key] conventions.");
        }
    }

    /// <summary>
    ///     Ensures that every root entity type with an explicit sort key annotation also has an
    ///     explicit partition key annotation.
    /// </summary>
    private static void ValidateSortKeyHasResolvablePartitionKey(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
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

    /// <summary>Validates that configured partition and sort key properties exist before EF base validation runs.</summary>
    private static void ValidateConfiguredKeyPropertiesExist(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            if (entityType[DynamoAnnotationNames.PartitionKeyPropertyName] is not string pkName)
                continue;

            _ = entityType.FindProperty(pkName)
                ?? throw new InvalidOperationException(
                    $"The partition key property '{pkName}' configured on entity type "
                    + $"'{entityType.DisplayName()}' does not exist.");

            ValidateConfiguredKeyPropertyIsSupported(entityType, pkName, "partition key");

            if (entityType[DynamoAnnotationNames.SortKeyPropertyName] is not string skName)
                continue;

            _ = entityType.FindProperty(skName)
                ?? throw new InvalidOperationException(
                    $"The sort key property '{skName}' configured on entity type "
                    + $"'{entityType.DisplayName()}' does not exist.");

            ValidateConfiguredKeyPropertyIsSupported(entityType, skName, "sort key");
        }
    }

    private static void ValidateConfiguredKeyPropertyIsSupported(
        IEntityType entityType,
        string propertyName,
        string keyRole)
    {
        var property = entityType.FindProperty(propertyName);
        if (property is null)
            return;

        if (property.IsRuntimeOnly())
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' configures property '{propertyName}' as DynamoDB {keyRole}, "
                + "but the property is runtime-only provider metadata and cannot be used as a table key.");

        if (property.IsShadowProperty())
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' configures property '{propertyName}' as DynamoDB {keyRole}, "
                + "but shadow key properties are not supported. Map the key to a CLR property (optionally with "
                + "a backing field) and call HasPartitionKey(...) / HasSortKey(...) on that member.");
    }

    /// <summary>
    ///     Validates that configured partition/sort key properties exist and that the EF primary key
    ///     matches exactly [PK] or [PK, SK].
    /// </summary>
    private static void ValidateKeyPropertyNames(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
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
                + $"the DynamoDB table key is [{expectedDisplay}]. Configure the EF primary key to match "
                + "the DynamoDB partition/sort key order after calling HasPartitionKey(...) and HasSortKey(...), "
                + "or use matching conventional key property names.");
        }
    }

    /// <summary>Validates that configured partition/sort key properties map to DynamoDB-compatible key provider types (string, number, or binary).</summary>
    private static void ValidateKeyPropertyTypes(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            var partitionKeyProperty = entityType.GetPartitionKeyProperty();
            if (partitionKeyProperty != null)
                ValidateSingleKeyPropertyType(entityType, partitionKeyProperty, "partition key");

            var sortKeyProperty = entityType.GetSortKeyProperty();
            if (sortKeyProperty != null)
                ValidateSingleKeyPropertyType(entityType, sortKeyProperty, "sort key");
        }
    }

    /// <summary>Validates a single DynamoDB key property has an allowed provider type category.</summary>
    private static void ValidateSingleKeyPropertyType(
        IEntityType entityType,
        IProperty keyProperty,
        string keyRole)
    {
        var effectiveProviderType = GetEffectiveProviderClrType(keyProperty);
        var typeCategory = GetKeyTypeCategory(effectiveProviderType);
        if (typeCategory is not DynamoKeyTypeCategory.Unsupported)
            return;

        throw new InvalidOperationException(
            $"Entity type '{entityType.DisplayName()}' maps property '{keyProperty.Name}' as DynamoDB {keyRole}, "
            + $"but the effective provider type '{effectiveProviderType.ShortDisplayName()}' is not supported. "
            + "DynamoDB partition and sort keys must be string, number, or binary (byte[]). "
            + "Configure a ValueConverter when the CLR key type differs from the stored key type.");
    }

    /// <summary>Validates that configured partition/sort key properties are required and cannot resolve to nullable provider types.</summary>
    private static void ValidateKeyPropertyNullability(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            var partitionKeyProperty = entityType.GetPartitionKeyProperty();
            if (partitionKeyProperty != null)
                ValidateSingleKeyPropertyNullability(
                    entityType,
                    partitionKeyProperty,
                    "partition key");

            var sortKeyProperty = entityType.GetSortKeyProperty();
            if (sortKeyProperty != null)
                ValidateSingleKeyPropertyNullability(entityType, sortKeyProperty, "sort key");
        }
    }

    /// <summary>Validates a single DynamoDB key property is non-nullable in CLR and provider form.</summary>
    private static void ValidateSingleKeyPropertyNullability(
        IEntityType entityType,
        IProperty keyProperty,
        string keyRole)
    {
        if (keyProperty.IsNullable)
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' maps property '{keyProperty.Name}' as DynamoDB {keyRole}, "
                + "but the property is nullable. DynamoDB key properties must be required and non-nullable.");

        var effectiveProviderType = GetEffectiveProviderClrTypeIncludingNullable(keyProperty);
        if (Nullable.GetUnderlyingType(effectiveProviderType) == null)
            return;

        throw new InvalidOperationException(
            $"Entity type '{entityType.DisplayName()}' maps property '{keyProperty.Name}' as DynamoDB {keyRole}, "
            + $"but the effective provider type '{effectiveProviderType.ShortDisplayName()}' is nullable. "
            + "DynamoDB key provider types must be non-nullable.");
    }

    /// <summary>Validates all root entity types sharing the same DynamoDB table agree on key schema.</summary>
    private static void ValidateTableKeySchemaConsistency(IModel model)
    {
        var tableGroups = new Dictionary<string, List<IEntityType>>(StringComparer.Ordinal);
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
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

    /// <summary>Validates discriminator mapping consistency for shared DynamoDB table groups.</summary>
    private static void ValidateDiscriminatorMappings(IModel model)
    {
        var tableGroups = new Dictionary<string, List<IEntityType>>(StringComparer.Ordinal);
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            var tableName = GetTableName(entityType) ?? entityType.ClrType.Name;
            if (!tableGroups.TryGetValue(tableName, out var group))
                tableGroups[tableName] = group = [];
            group.Add(entityType);
        }

        foreach (var (tableName, rootEntityTypes) in tableGroups)
            ValidateDiscriminatorMappingGroup(tableName, rootEntityTypes);
    }

    /// <summary>Validates discriminator requirements and collisions for a single shared-table group.</summary>
    private static void ValidateDiscriminatorMappingGroup(
        string tableName,
        List<IEntityType> rootEntityTypes)
    {
        HashSet<IEntityType> concreteTypes = [];
        foreach (var rootEntityType in rootEntityTypes)
        {
            foreach (var concreteType in rootEntityType.GetConcreteDerivedTypesInclusive())
            {
                if (concreteType.IsOwned() || concreteType.FindOwnership() is not null)
                    continue;

                if (concreteType.ClrType.IsAbstract)
                    continue;

                concreteTypes.Add(concreteType);
            }
        }

        if (concreteTypes.Count <= 1)
            return;

        var concreteTypesWithDiscriminator = concreteTypes
            .Where(static entityType => entityType.FindDiscriminatorProperty() is not null)
            .OrderBy(static entityType => entityType.DisplayName(), StringComparer.Ordinal)
            .ToList();

        if (concreteTypesWithDiscriminator.Count == 0)
        {
            if (rootEntityTypes.All(IsDiscriminatorDisabled))
                return;

            throw new InvalidOperationException(
                $"Entity types mapped to shared DynamoDB table '{tableName}' do not define a discriminator property. "
                + "Shared-table mappings with multiple entity types require a discriminator by default. "
                + "Call HasNoDiscriminator() for the table group to explicitly opt out.");
        }

        // Mixed state (some with discriminator, some without) is normalized by
        // DynamoDiscriminatorConvention before finalization — if any root in the group has an
        // explicit no-discriminator signal, the convention propagates HasNoDiscriminator() to the
        // entire group. By the time validation runs, all concrete types either have a discriminator
        // property or none do, so concreteTypesWithDiscriminator.Count == concreteTypes.Count here.

        var first = concreteTypesWithDiscriminator[0];
        var expectedDiscriminatorProperty = first.FindDiscriminatorProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{first.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' but does not define a discriminator property. "
                + "Shared-table mappings with multiple entity types require a discriminator property.");

        var expectedDiscriminatorValue = first.GetDiscriminatorValue()
            ?? throw new InvalidOperationException(
                $"Entity type '{first.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' but does not define a discriminator value. "
                + "Shared-table mappings with multiple entity types require a discriminator value.");

        var expectedDiscriminatorAttributeName = expectedDiscriminatorProperty.GetAttributeName();

        var valuesByEntityType = new Dictionary<object, IEntityType>(
            expectedDiscriminatorProperty.GetKeyValueComparer());
        valuesByEntityType[expectedDiscriminatorValue] = first;

        foreach (var entityType in concreteTypesWithDiscriminator.Skip(1))
        {
            var discriminatorProperty = entityType.FindDiscriminatorProperty()
                ?? throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' but does not define a discriminator property. "
                    + "Shared-table mappings with multiple entity types require a discriminator property.");

            var discriminatorAttributeName = discriminatorProperty.GetAttributeName();
            if (!string.Equals(
                discriminatorAttributeName,
                expectedDiscriminatorAttributeName,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{entityType.DisplayName()}' are both mapped to shared DynamoDB table '{tableName}' "
                    + $"but use different discriminator attribute names ('{expectedDiscriminatorAttributeName}' vs '{discriminatorAttributeName}'). "
                    + "All entity types sharing a table must use the same discriminator attribute name.");

            var discriminatorValue = entityType.GetDiscriminatorValue()
                ?? throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' but does not define a discriminator value. "
                    + "Shared-table mappings with multiple entity types require a discriminator value.");

            if (!valuesByEntityType.TryAdd(discriminatorValue, entityType))
            {
                var existingEntityType = valuesByEntityType[discriminatorValue];
                throw new InvalidOperationException(
                    $"Entity types '{existingEntityType.DisplayName()}' and '{entityType.DisplayName()}' are both mapped to shared DynamoDB table '{tableName}' "
                    + $"and use duplicate discriminator value '{discriminatorValue}'. Discriminator values must be unique within a shared table.");
            }
        }

        ValidateDiscriminatorKeyNameCollisions(
            tableName,
            rootEntityTypes,
            expectedDiscriminatorAttributeName);
    }

    private static bool IsDiscriminatorDisabled(IEntityType entityType)
        => entityType[DynamoAnnotationNames.DiscriminatorDisabled] as bool? == true;

    /// <summary>
    ///     Validates that the shared-table discriminator attribute does not collide with PK/SK
    ///     attributes.
    /// </summary>
    private static void ValidateDiscriminatorKeyNameCollisions(
        string tableName,
        IEnumerable<IEntityType> rootEntityTypes,
        string discriminatorAttributeName)
    {
        foreach (var entityType in rootEntityTypes)
        {
            var partitionKeyAttributeName =
                entityType.GetPartitionKeyProperty()?.GetAttributeName();
            if (string.Equals(
                partitionKeyAttributeName,
                discriminatorAttributeName,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' and uses discriminator attribute name '{discriminatorAttributeName}', "
                    + "which collides with the partition key attribute name. Discriminator attributes must not reuse PK attribute names.");

            var sortKeyAttributeName = entityType.GetSortKeyProperty()?.GetAttributeName();
            if (string.Equals(
                sortKeyAttributeName,
                discriminatorAttributeName,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' is mapped to shared DynamoDB table '{tableName}' and uses discriminator attribute name '{discriminatorAttributeName}', "
                    + "which collides with the sort key attribute name. Discriminator attributes must not reuse SK attribute names.");
        }
    }

    /// <summary>Validates PK/SK name consistency within a single table group.</summary>
    private static void ValidateTableKeySchemaGroup(string tableName, List<IEntityType> entityTypes)
    {
        var first = entityTypes[0];
        var expectedPkProperty = first.GetPartitionKeyProperty();
        var expectedSkProperty = first.GetSortKeyProperty();
        var expectedPk = expectedPkProperty?.GetAttributeName();
        var expectedSk = expectedSkProperty?.GetAttributeName();
        var expectedPkTypeCategory =
            GetKeyTypeCategory(GetEffectiveProviderClrType(expectedPkProperty!));
        var expectedSkTypeCategory = expectedSkProperty == null
            ? DynamoKeyTypeCategory.Unsupported
            : GetKeyTypeCategory(GetEffectiveProviderClrType(expectedSkProperty));

        for (var i = 1; i < entityTypes.Count; i++)
        {
            var et = entityTypes[i];
            var pkProperty = et.GetPartitionKeyProperty();
            var skProperty = et.GetSortKeyProperty();
            var pk = pkProperty?.GetAttributeName();
            var sk = skProperty?.GetAttributeName();

            if (!string.Equals(pk, expectedPk, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' but have different partition key attribute names ('{expectedPk ?? "<none>"}' vs '{pk ?? "<none>"}'). "
                    + "All entity types sharing a DynamoDB table must use the same partition key attribute name.");

            if (expectedSk == null != (sk == null))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' but use mixed key shapes (one is PK-only and the other is PK+SK). "
                    + "All entity types sharing a DynamoDB table must agree on key shape.");

            if (!string.Equals(sk, expectedSk, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' but have inconsistent sort key attribute names "
                    + $"('{expectedSk ?? "<none>"}' vs '{sk ?? "<none>"}')."
                    + " All entity types sharing a DynamoDB table must agree on the sort key attribute name.");

            var pkTypeCategory = GetKeyTypeCategory(GetEffectiveProviderClrType(pkProperty!));
            if (pkTypeCategory != expectedPkTypeCategory)
                throw new InvalidOperationException(
                    $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                    + $"'{tableName}' and partition key attribute '{expectedPk ?? "<none>"}', but use different key type categories "
                    + $"('{expectedPkTypeCategory}' vs '{pkTypeCategory}'). All entity types sharing a DynamoDB table "
                    + "must use the same key type category for the same partition key attribute.");

            if (expectedSkProperty == null || skProperty == null)
                continue;

            var skTypeCategory = GetKeyTypeCategory(GetEffectiveProviderClrType(skProperty));
            if (skTypeCategory == expectedSkTypeCategory)
                continue;

            throw new InvalidOperationException(
                $"Entity types '{first.DisplayName()}' and '{et.DisplayName()}' are both mapped to table "
                + $"'{tableName}' and sort key attribute '{expectedSk ?? "<none>"}', but use different key type categories "
                + $"('{expectedSkTypeCategory}' vs '{skTypeCategory}'). All entity types sharing a DynamoDB table "
                + "must use the same key type category for the same sort key attribute.");
        }
    }

    /// <summary>Validates DynamoDB secondary-index prerequisites and key-shape rules.</summary>
    private static void ValidateSecondaryIndexes(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
        {
            foreach (var index in entityType.EnumerateSecondaryIndexesInHierarchy())
            {
                var indexKind = index.GetSecondaryIndexKind();
                if (indexKind is null)
                    continue;

                switch (indexKind)
                {
                    case DynamoSecondaryIndexKind.Global:
                        ValidateGlobalSecondaryIndex(index);
                        break;
                    case DynamoSecondaryIndexKind.Local:
                        ValidateLocalSecondaryIndex(entityType, index);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Entity type '{index.DeclaringEntityType.DisplayName()}' configures secondary index '{GetSecondaryIndexDisplayName(index)}' "
                            + $"with unsupported index kind '{indexKind}'.");
                }
            }
        }
    }

    /// <summary>Validates that a configured global secondary index has a supported key shape.</summary>
    private static void ValidateGlobalSecondaryIndex(IReadOnlyIndex index)
    {
        var declaringEntityDisplayName = index.DeclaringEntityType.DisplayName();
        var indexName = GetSecondaryIndexDisplayName(index);
        if (index.Properties.Count is < 1 or > 2)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures global secondary index '{indexName}', "
                + $"but the GSI metadata contains {index.Properties.Count} key properties. Global secondary indexes must define one partition key and optional sort key.");

        var globalPartitionKeyProperty = index.Properties[0];
        ValidateSecondaryIndexKeyPropertyType(
            declaringEntityDisplayName,
            index,
            globalPartitionKeyProperty,
            "global secondary index partition key");

        if (index.Properties.Count == 2)
        {
            var globalSortKeyProperty = index.Properties[1];
            if (globalSortKeyProperty == globalPartitionKeyProperty)
                throw new InvalidOperationException(
                    $"Entity type '{declaringEntityDisplayName}' configures global secondary index '{indexName}' using property '{globalSortKeyProperty.Name}' for both partition and sort keys, "
                    + "but a global secondary index must use distinct partition and sort key attributes.");

            var globalPartitionKeyAttributeName = globalPartitionKeyProperty.GetAttributeName();
            var globalSortKeyAttributeName = globalSortKeyProperty.GetAttributeName();
            if (string.Equals(
                globalPartitionKeyAttributeName,
                globalSortKeyAttributeName,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Entity type '{declaringEntityDisplayName}' configures global secondary index '{indexName}' with partition key '{globalPartitionKeyProperty.Name}' and sort key '{globalSortKeyProperty.Name}', "
                    + $"but both resolve to attribute name '{globalPartitionKeyAttributeName}'. Global secondary indexes must use distinct partition and sort key attributes.");

            ValidateSecondaryIndexKeyPropertyType(
                declaringEntityDisplayName,
                index,
                globalSortKeyProperty,
                "global secondary index sort key");
        }
    }

    /// <summary>Validates that a configured local secondary index has a valid alternate sort key.</summary>
    private static void ValidateLocalSecondaryIndex(IEntityType entityType, IReadOnlyIndex index)
    {
        var declaringEntityDisplayName = index.DeclaringEntityType.DisplayName();
        var indexName = GetSecondaryIndexDisplayName(index);
        var partitionKeyProperty = entityType.GetPartitionKeyProperty();
        if (partitionKeyProperty is null)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}', "
                + "but no DynamoDB partition key is configured. Configure HasPartitionKey(...) or use a conventional partition key property name before configuring an LSI.");

        var sortKeyProperty = entityType.GetSortKeyProperty();
        if (sortKeyProperty is null)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}', "
                + "but no DynamoDB sort key is configured. Configure HasSortKey(...) or use a conventional sort key property name before configuring an LSI.");

        if (index.Properties.Count != 1)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}', "
                + $"but the LSI metadata contains {index.Properties.Count} key properties. Local secondary indexes must define exactly one alternate sort key property.");

        var localSortKeyProperty = index.Properties[0];
        if (localSortKeyProperty == sortKeyProperty)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}' using property '{localSortKeyProperty.Name}', "
                + "but a local secondary index must use an alternate sort key different from the table sort key.");

        if (localSortKeyProperty == partitionKeyProperty)
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}' using property '{localSortKeyProperty.Name}', "
                + "but a local secondary index must use an alternate sort key different from the table partition key.");

        var localSortKeyAttributeName = localSortKeyProperty.GetAttributeName();
        var tablePartitionKeyAttributeName = partitionKeyProperty.GetAttributeName();
        if (string.Equals(
            localSortKeyAttributeName,
            tablePartitionKeyAttributeName,
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}' with alternate sort key '{localSortKeyProperty.Name}', "
                + $"but it resolves to partition key attribute name '{tablePartitionKeyAttributeName}'. Local secondary indexes must use an alternate sort key attribute different from the table partition key attribute.");

        var tableSortKeyAttributeName = sortKeyProperty.GetAttributeName();
        if (string.Equals(
            localSortKeyAttributeName,
            tableSortKeyAttributeName,
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Entity type '{declaringEntityDisplayName}' configures local secondary index '{indexName}' with alternate sort key '{localSortKeyProperty.Name}', "
                + $"but it resolves to sort key attribute name '{tableSortKeyAttributeName}'. Local secondary indexes must use an alternate sort key attribute different from the table sort key attribute.");

        ValidateSecondaryIndexKeyPropertyType(
            declaringEntityDisplayName,
            index,
            localSortKeyProperty,
            "local secondary index sort key");
    }

    /// <summary>Validates a secondary-index key property resolves to a DynamoDB key-compatible provider type.</summary>
    private static void ValidateSecondaryIndexKeyPropertyType(
        string declaringEntityDisplayName,
        IReadOnlyIndex index,
        IReadOnlyProperty keyProperty,
        string keyRole)
    {
        var effectiveProviderType = GetEffectiveProviderClrType(keyProperty);
        var typeCategory = GetKeyTypeCategory(effectiveProviderType);
        if (typeCategory is not DynamoKeyTypeCategory.Unsupported)
            return;

        throw new InvalidOperationException(
            $"Entity type '{declaringEntityDisplayName}' configures secondary index '{GetSecondaryIndexDisplayName(index)}' "
            + $"with property '{keyProperty.Name}' as DynamoDB {keyRole}, "
            + $"but the effective provider type '{effectiveProviderType.ShortDisplayName()}' is not supported. "
            + "DynamoDB secondary index keys must be string, number, or binary (byte[]). "
            + "Configure a ValueConverter when the CLR key type differs from the stored key type.");
    }

    /// <summary>Gets a stable display name for secondary-index validation messages.</summary>
    private static string GetSecondaryIndexDisplayName(IReadOnlyIndex index)
        => index.GetSecondaryIndexName() ?? index.Name ?? "<unnamed>";

    /// <summary>
    ///     Overrides EF Core's generic unmapped-property error with a DynamoDB-specific message that
    ///     names supported wire types and suggests <c>HasConversion</c> as the fix.
    /// </summary>
    /// <remarks>
    ///     EF Core's base <c>ValidatePropertyMapping</c> calls this virtual when it finds an
    ///     explicitly-configured property with no type mapping. Overriding it here lets usDbLoggerCategory.Model.Validationpecific message before the generic EF error is emitted.
    /// </remarks>
    protected override void
        ThrowPropertyNotMappedException(
            string propertyType,
            IConventionTypeBase structuralType,
            IConventionProperty unmappedProperty)
        => throw new InvalidOperationException(
            $"Property '{structuralType.DisplayName()}.{unmappedProperty.Name}' of CLR type "
            + $"'{propertyType}' cannot be mapped because DynamoDB does not support this type. "
            + "DynamoDB supports: string, bool, byte[], numeric types (byte, short, int, long, "
            + "float, double, decimal), and nullable variants, as well as collections of these types. "
            + "If this is a custom CLR type, configure a value converter: "
            + ".HasConversion<string>() or .HasConversion<long>(). "
            + "Alternatively, exclude the property with [NotMapped] or Ignore().");

    /// <summary>Validates that every scalar property in the model can be serialized by this provider.</summary>
    private static void ValidateScalarPropertyTypeMappings(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            ValidateTypeBaseScalarMappings(entityType);
            foreach (var complexProperty in entityType.GetDeclaredComplexProperties())
                ValidateComplexTypeScalarMappings(complexProperty.ComplexType);
        }
    }

    /// <summary>Recursively validates scalar property mappings on a complex type graph.</summary>
    private static void ValidateComplexTypeScalarMappings(IComplexType complexType)
    {
        ValidateTypeBaseScalarMappings(complexType);
        foreach (var complexProperty in complexType.GetDeclaredComplexProperties())
            ValidateComplexTypeScalarMappings(complexProperty.ComplexType);
    }

    /// <summary>
    ///     Validates that every declared scalar property on a type base has a serializable DynamoDB
    ///     mapping.
    /// </summary>
    private static void ValidateTypeBaseScalarMappings(ITypeBase typeBase)
    {
        foreach (var property in typeBase.GetDeclaredProperties())
        {
            // Runtime-only provider metadata is populated outside DynamoDB attribute mapping and
            // never serialized to the store.
            if (property.IsRuntimeOnly())
                continue;

            // Null-mapping and non-DynamoTypeMapping cases are caught earlier by EF Core's
            // ValidatePropertyMapping via our ThrowPropertyNotMappedException override. Skip
            // those here; only check CanWriteToAttributeValue as defense-in-depth for mappings that
            // exist
            // but cannot be serialized to DynamoDB wire format.
            if (property.FindTypeMapping() is not DynamoTypeMapping dynamoMapping)
                continue;

            if (!dynamoMapping.CanWriteToAttributeValue)
                throw new InvalidOperationException(
                    $"Property '{typeBase.DisplayName()}.{property.Name}' of CLR type "
                    + $"'{property.ClrType.Name}' cannot be serialized to DynamoDB. "
                    + "DynamoDB supports: string, bool, byte[], numeric types (byte, short, int, "
                    + "long, float, double, decimal), and nullable variants. "
                    + "To map this type, configure a value converter: "
                    + ".HasConversion<string>() or another supported provider type.");

            if (!property.IsPrimitiveCollection)
                continue;

            // For primitive collections, also verify the element type can be serialized.
            // The collection mapping's CanWriteToAttributeValue already reflects this, but checking
            // the element mapping separately produces a more targeted error message.
            var elementType = property.GetElementType();
            if (elementType?.FindTypeMapping() is DynamoTypeMapping elementMapping
                && !elementMapping.CanWriteToAttributeValue)
                throw new InvalidOperationException(
                    $"Primitive collection property '{typeBase.DisplayName()}.{property.Name}' "
                    + $"of CLR type '{property.ClrType.Name}' has element type "
                    + $"'{elementType.ClrType.Name}' which cannot be serialized to DynamoDB. "
                    + "Primitive collection elements must be DynamoDB-supported scalars: string, "
                    + "bool, byte[], and numeric types. Configure a value converter on the "
                    + "element type to map it to a supported scalar.");
        }
    }

    /// <summary>Validates DynamoDB optimistic concurrency configuration.</summary>
    /// <remarks>
    ///     This provider currently supports manual concurrency tokens only:
    ///     <c>.IsConcurrencyToken()</c> / <c>[ConcurrencyCheck]</c>. Row-version semantics (
    ///     <c>ValueGenerated.OnAddOrUpdate</c>, including <c>IsRowVersion()</c>) are not provider-managed
    ///     yet and are rejected at model-validation time.
    /// </remarks>
    private static void ValidateConcurrencyTokenConfiguration(IModel model)
    {
        foreach (var entityType in EnumerateRootEntityTypes(model))
            ValidateConcurrencyTokensOnEntityType(entityType);

        // Owned entity types are excluded from EnumerateRootEntityTypes but can also carry
        // concurrency tokens — validate them separately.
        foreach (var ownedType in model.GetEntityTypes().Where(static t => t.IsOwned()))
            ValidateConcurrencyTokensOnEntityType(ownedType);
    }

    /// <summary>
    ///     Throws if any declared concurrency token on <paramref name="entityType" /> uses
    ///     row-version semantics (<see cref="ValueGenerated.OnAddOrUpdate" />), which the provider does
    ///     not support.
    /// </summary>
    private static void ValidateConcurrencyTokensOnEntityType(IEntityType entityType)
    {
        foreach (var property in entityType.GetDeclaredProperties())
        {
            if (!property.IsConcurrencyToken)
                continue;

            if (property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                throw new InvalidOperationException(
                    $"Property '{entityType.DisplayName()}.{property.Name}' is configured "
                    + "as a row-version token (ValueGenerated.OnAddOrUpdate / IsRowVersion()), "
                    + "but the DynamoDB provider does not currently support provider-managed "
                    + "row-version value generation. Configure this property with "
                    + "IsConcurrencyToken() only and update the token value in application "
                    + "code before saving changes.");
        }
    }

    /// <summary>Returns root entity types that participate in top-level DynamoDB table mappings.</summary>
    private static IEnumerable<IEntityType> EnumerateRootEntityTypes(IModel model)
        => model
            .GetEntityTypes()
            .Where(static entityType
                => !entityType.IsOwned()
                && entityType.FindOwnership() == null
                && entityType.BaseType == null);

    /// <summary>Returns the effective provider CLR type with nullable wrappers removed.</summary>
    private static Type GetEffectiveProviderClrType(IReadOnlyProperty property)
    {
        var providerType = GetEffectiveProviderClrTypeIncludingNullable(property);
        return Nullable.GetUnderlyingType(providerType) ?? providerType;
    }

    /// <summary>Returns the effective provider CLR type and preserves nullable wrappers.</summary>
    private static Type GetEffectiveProviderClrTypeIncludingNullable(IReadOnlyProperty property)
        => property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;

    /// <summary>Maps a CLR type to the DynamoDB key type categories used in validation.</summary>
    private static DynamoKeyTypeCategory GetKeyTypeCategory(Type clrType)
        => clrType == typeof(string) ? DynamoKeyTypeCategory.String :
            clrType == typeof(byte[]) ? DynamoKeyTypeCategory.Binary :
            IsNumericType(clrType) ? DynamoKeyTypeCategory.Number :
            DynamoKeyTypeCategory.Unsupported;

    /// <summary>Determines whether a CLR type is treated as a DynamoDB numeric key type.</summary>
    private static bool IsNumericType(Type clrType)
        => Type.GetTypeCode(clrType) is TypeCode.Byte
            or TypeCode.SByte
            or TypeCode.Int16
            or TypeCode.UInt16
            or TypeCode.Int32
            or TypeCode.UInt32
            or TypeCode.Int64
            or TypeCode.UInt64
            or TypeCode.Single
            or TypeCode.Double
            or TypeCode.Decimal;

    /// <summary>Represents supported DynamoDB key type categories for model validation.</summary>
    private enum DynamoKeyTypeCategory
    {
        Unsupported,
        String,
        Number,
        Binary,
    }

    /// <summary>Gets the DynamoDB table name annotation configured for an entity type.</summary>
    private static string? GetTableName(IReadOnlyEntityType entityType)
        => entityType[DynamoAnnotationNames.TableName] as string;
}
