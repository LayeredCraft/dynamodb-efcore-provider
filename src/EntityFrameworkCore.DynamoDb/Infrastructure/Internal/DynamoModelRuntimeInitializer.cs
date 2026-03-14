using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Builds runtime DynamoDB table metadata after model validation completes.</summary>
public sealed class DynamoModelRuntimeInitializer(ModelRuntimeInitializerDependencies dependencies)
    : ModelRuntimeInitializer(dependencies)
{
    /// <summary>Attaches the canonical runtime table model to the finalized runtime model.</summary>
    /// <remarks>
    ///     Overrides <c>ModelRuntimeInitializer.InitializeModel</c> rather than
    ///     <c>ModelRuntimeInitializer.Initialize</c> to avoid depending on the undocumented
    ///     return-value contract of <c>Initialize</c> changing based on <paramref name="designTime"/>.
    ///     The <c>prevalidation=false</c> pass runs after model validation, so all metadata is
    ///     guaranteed to be consistent when the runtime descriptors are built.
    /// </remarks>
    protected override void InitializeModel(IModel model, bool designTime, bool prevalidation)
    {
        base.InitializeModel(model, designTime, prevalidation);

        if (prevalidation)
            return;

        model.GetOrAddRuntimeAnnotationValue(
            DynamoAnnotationNames.RuntimeTableModel,
            static currentModel => BuildRuntimeTableModel((IReadOnlyModel)currentModel!),
            model);
    }

    /// <summary>Builds runtime table descriptors grouped by effective physical table name.</summary>
    private static DynamoRuntimeTableModel BuildRuntimeTableModel(IReadOnlyModel model)
    {
        Dictionary<string, DynamoTableDescriptor> tables = new(StringComparer.Ordinal);

        foreach (var tableGroup in model
            .EnumerateRootEntityTypes()
            .GroupBy(static entityType => entityType.GetTableGroupName(), StringComparer.Ordinal))
        {
            var rootEntityTypes = tableGroup.ToList();
            var sourcesByRootEntityType =
                new Dictionary<string, IReadOnlyList<DynamoIndexDescriptor>>(
                    StringComparer.Ordinal);
            var sourcesByQueryEntityType =
                new Dictionary<string, IReadOnlyList<DynamoIndexDescriptor>>(
                    StringComparer.Ordinal);

            foreach (var rootEntityType in rootEntityTypes)
            {
                sourcesByRootEntityType[rootEntityType.Name] = BuildSourceDescriptors(
                    rootEntityType,
                    rootEntityType.EnumerateSecondaryIndexesInHierarchy());

                foreach (var queryEntityType in rootEntityType.GetDerivedTypesInclusive())
                    sourcesByQueryEntityType[queryEntityType.Name] = BuildSourceDescriptors(
                        queryEntityType,
                        EnumerateSecondaryIndexesForQueryEntity(queryEntityType));
            }

            ValidateSharedTableSources(tableGroup.Key, sourcesByRootEntityType);

            tables[tableGroup.Key] = new DynamoTableDescriptor(
                tableGroup.Key,
                rootEntityTypes,
                sourcesByRootEntityType,
                sourcesByQueryEntityType);
        }

        return new DynamoRuntimeTableModel(tables);
    }

    /// <summary>Builds the ordered source descriptors visible to a single queryable entity type.</summary>
    private static IReadOnlyList<DynamoIndexDescriptor> BuildSourceDescriptors(
        IReadOnlyEntityType entityType,
        IEnumerable<IReadOnlyIndex> secondaryIndexes)
    {
        var sourceEntityType = ResolveKeySourceEntityType(entityType);
        var partitionKeyProperty = sourceEntityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not have a configured DynamoDB partition key.");

        List<DynamoIndexDescriptor> sources =
        [
            new DynamoIndexDescriptor(
                null,
                DynamoIndexSourceKind.Table,
                null,
                partitionKeyProperty,
                sourceEntityType.GetSortKeyProperty(),
                DynamoSecondaryIndexProjectionType.All),
        ];

        Dictionary<string, DynamoIndexDescriptor> secondaryIndexesByName =
            new(StringComparer.Ordinal);

        foreach (var index in secondaryIndexes)
        {
            var secondaryIndexKind = index.GetSecondaryIndexKind();
            if (secondaryIndexKind is null)
                continue;

            var indexName = index.GetSecondaryIndexName()
                ?? throw new InvalidOperationException(
                    $"Secondary index '{index.Name ?? "<unnamed>"}' on entity type '{index.DeclaringEntityType.DisplayName()}' is missing a DynamoDB index name.");

            var descriptor = secondaryIndexKind switch
            {
                DynamoSecondaryIndexKind.Global => BuildGlobalSecondaryIndexDescriptor(
                    index,
                    indexName),
                DynamoSecondaryIndexKind.Local => BuildLocalSecondaryIndexDescriptor(
                    partitionKeyProperty,
                    index,
                    indexName),
                _ => throw new InvalidOperationException(
                    $"Secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' has an unsupported kind '{secondaryIndexKind}'."),
            };

            if (!secondaryIndexesByName.TryGetValue(indexName, out var existingDescriptor))
            {
                secondaryIndexesByName[indexName] = descriptor;
                sources.Add(descriptor);
                continue;
            }

            if (HasEquivalentSourceSignature(existingDescriptor, descriptor))
                continue;

            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' defines secondary index name '{indexName}' multiple times with conflicting metadata. "
                + "Index names must be unique per DynamoDB table and map to a single index definition.");
        }

        return sources;
    }

    /// <summary>
    ///     Enumerates secondary indexes visible to the queried entity type by combining the base
    ///     hierarchy chain with the queried type itself.
    /// </summary>
    /// <remarks>
    ///     Query-time source selection must stay result-complete for the queried entity set. A
    ///     secondary index declared only on a derived subtype may be sparse for sibling/base
    ///     entities, so exposing it to base-type queries would allow auto-selection to drop rows.
    /// </remarks>
    private static IEnumerable<IReadOnlyIndex>
        EnumerateSecondaryIndexesForQueryEntity(IReadOnlyEntityType entityType)
        => entityType
            .GetAllBaseTypes()
            .Append(entityType)
            .SelectMany(static type => type.GetDeclaredIndexes())
            .Where(static index => index.GetSecondaryIndexKind() is not null)
            .OrderBy(static index => index.GetSecondaryIndexName(), StringComparer.Ordinal)
            .ThenBy(static index => index.DeclaringEntityType.Name, StringComparer.Ordinal);

    /// <summary>Builds a runtime descriptor for a global secondary index.</summary>
    private static DynamoIndexDescriptor BuildGlobalSecondaryIndexDescriptor(
        IReadOnlyIndex index,
        string indexName)
    {
        if (index.Properties.Count is < 1 or > 2)
        {
            throw new InvalidOperationException(
                $"Global secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' must define one or two key properties.");
        }

        return new DynamoIndexDescriptor(
            indexName,
            DynamoIndexSourceKind.GlobalSecondaryIndex,
            index,
            index.Properties[0],
            index.Properties.Count == 2 ? index.Properties[1] : null,
            index.GetSecondaryIndexProjectionType() ?? DynamoSecondaryIndexProjectionType.All);
    }

    /// <summary>Builds a runtime descriptor for a local secondary index.</summary>
    private static DynamoIndexDescriptor BuildLocalSecondaryIndexDescriptor(
        IReadOnlyProperty partitionKeyProperty,
        IReadOnlyIndex index,
        string indexName)
    {
        if (index.Properties.Count != 1)
        {
            throw new InvalidOperationException(
                $"Local secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' must define exactly one alternate sort-key property.");
        }

        return new DynamoIndexDescriptor(
            indexName,
            DynamoIndexSourceKind.LocalSecondaryIndex,
            index,
            partitionKeyProperty,
            index.Properties[0],
            index.GetSecondaryIndexProjectionType() ?? DynamoSecondaryIndexProjectionType.All);
    }

    /// <summary>Ensures shared-table root entity types do not define conflicting secondary indexes.</summary>
    private static void ValidateSharedTableSources(
        string tableName,
        IReadOnlyDictionary<string, IReadOnlyList<DynamoIndexDescriptor>> sourcesByRootEntityType)
    {
        var entityTypeNames =
            sourcesByRootEntityType
                .Keys
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();

        for (var i = 0; i < entityTypeNames.Length; i++)
        {
            for (var j = i + 1; j < entityTypeNames.Length; j++)
            {
                var leftEntityTypeName = entityTypeNames[i];
                var rightEntityTypeName = entityTypeNames[j];

                if (HaveCompatibleSharedTableSources(
                    sourcesByRootEntityType[leftEntityTypeName],
                    sourcesByRootEntityType[rightEntityTypeName]))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Entity types '{leftEntityTypeName}' and '{rightEntityTypeName}' map to DynamoDB table '{tableName}' but expose inconsistent secondary-index metadata.");
            }
        }
    }

    /// <summary>
    ///     Determines whether two source lists expose compatible shared-table secondary-index metadata.
    /// </summary>
    /// <remarks>
    ///     Shared-table entity types may expose different secondary-index sets. Compatibility only
    ///     requires agreement for overlapping secondary indexes with the same index name.
    /// </remarks>
    private static bool HaveCompatibleSharedTableSources(
        IReadOnlyList<DynamoIndexDescriptor> left,
        IReadOnlyList<DynamoIndexDescriptor> right)
    {
        var rightByName = right
            .Where(static descriptor => descriptor.IndexName is not null)
            .ToDictionary(static descriptor => descriptor.IndexName!, StringComparer.Ordinal);

        foreach (var leftDescriptor in left.Where(static descriptor
            => descriptor.IndexName is not null))
        {
            if (!rightByName.TryGetValue(leftDescriptor.IndexName!, out var rightDescriptor))
                continue;

            if (!HasEquivalentSourceSignature(leftDescriptor, rightDescriptor))
                return false;
        }

        return true;
    }

    /// <summary>Determines whether two source descriptors are equivalent for shared-table analysis.</summary>
    private static bool HasEquivalentSourceSignature(
        DynamoIndexDescriptor left,
        DynamoIndexDescriptor right)
        => left.IndexName == right.IndexName
            && left.Kind == right.Kind
            && left.ProjectionType == right.ProjectionType
            && left.PartitionKeyProperty.GetAttributeName()
            == right.PartitionKeyProperty.GetAttributeName()
            && left.SortKeyProperty?.GetAttributeName() == right.SortKeyProperty?.GetAttributeName()
            && GetKeyTypeCategory(GetEffectiveProviderClrType(left.PartitionKeyProperty))
            == GetKeyTypeCategory(GetEffectiveProviderClrType(right.PartitionKeyProperty))
            && GetSortKeyTypeCategory(left.SortKeyProperty)
            == GetSortKeyTypeCategory(right.SortKeyProperty);

    /// <summary>Resolves the entity type that defines table key metadata for the queried entity type.</summary>
    private static IReadOnlyEntityType ResolveKeySourceEntityType(IReadOnlyEntityType entityType)
    {
        var tableMappedType = entityType.ResolveTableMappedEntityType();
        if (tableMappedType.GetPartitionKeyProperty() is not null)
            return tableMappedType;

        foreach (var baseType in entityType.GetAllBaseTypes())
            if (baseType.GetPartitionKeyProperty() is not null)
                return baseType;

        return tableMappedType;
    }

    /// <summary>Returns the effective provider CLR type with nullable wrappers removed.</summary>
    private static Type GetEffectiveProviderClrType(IReadOnlyProperty property)
    {
        var providerType = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
        return Nullable.GetUnderlyingType(providerType) ?? providerType;
    }

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

    /// <summary>Gets the key type category for an optional sort key property.</summary>
    private static DynamoKeyTypeCategory GetSortKeyTypeCategory(IReadOnlyProperty? property)
        => property is null
            ? DynamoKeyTypeCategory.Unsupported
            : GetKeyTypeCategory(GetEffectiveProviderClrType(property));

    /// <summary>Represents supported DynamoDB key type categories for runtime consistency checks.</summary>
    private enum DynamoKeyTypeCategory
    {
        Unsupported,
        String,
        Number,
        Binary,
    }
}
