using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Builds runtime DynamoDB table metadata after model validation completes.</summary>
public sealed class DynamoModelRuntimeInitializer(
    ModelRuntimeInitializerDependencies dependencies)
    : ModelRuntimeInitializer(dependencies)
{
    /// <summary>Attaches the canonical runtime table model to the finalized runtime model.</summary>
    /// <remarks>
    ///     Overrides <see cref="ModelRuntimeInitializer.InitializeModel"/> rather than
    ///     <see cref="ModelRuntimeInitializer.Initialize"/> to avoid depending on the undocumented
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
            static currentModel => BuildRuntimeTableModel((IReadOnlyModel)currentModel),
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
            var sourcesByEntityType = new Dictionary<string, IReadOnlyList<DynamoIndexDescriptor>>(StringComparer.Ordinal);

            foreach (var rootEntityType in rootEntityTypes)
                sourcesByEntityType[rootEntityType.Name] = BuildSourceDescriptors(rootEntityType);

            ValidateSharedTableSources(tableGroup.Key, sourcesByEntityType);

            tables[tableGroup.Key] = new DynamoTableDescriptor(
                tableGroup.Key,
                rootEntityTypes,
                sourcesByEntityType);
        }

        return new DynamoRuntimeTableModel(tables);
    }

    /// <summary>Builds the ordered source descriptors for a single mapped root entity type.</summary>
    private static IReadOnlyList<DynamoIndexDescriptor> BuildSourceDescriptors(IReadOnlyEntityType entityType)
    {
        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not have a configured DynamoDB partition key.");

        List<DynamoIndexDescriptor> sources =
        [
            new DynamoIndexDescriptor(
                null,
                DynamoIndexSourceKind.Table,
                null,
                partitionKeyProperty,
                entityType.GetSortKeyProperty(),
                DynamoSecondaryIndexProjectionType.All),
        ];

        foreach (var index in GetSecondaryIndexes(entityType))
        {
            var secondaryIndexKind = index.GetSecondaryIndexKind();
            if (secondaryIndexKind is null)
                continue;

            var indexName = index.GetSecondaryIndexName()
                ?? throw new InvalidOperationException(
                    $"Secondary index '{index.Name ?? "<unnamed>"}' on entity type '{index.DeclaringEntityType.DisplayName()}' is missing a DynamoDB index name.");

            sources.Add(secondaryIndexKind switch
            {
                DynamoSecondaryIndexKind.Global => BuildGlobalSecondaryIndexDescriptor(index, indexName),
                DynamoSecondaryIndexKind.Local => BuildLocalSecondaryIndexDescriptor(entityType, index, indexName),
                _ => throw new InvalidOperationException(
                    $"Secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' has an unsupported kind '{secondaryIndexKind}'."),
            });
        }

        return sources;
    }

    /// <summary>Enumerates configured secondary indexes across a mapped hierarchy in deterministic order.</summary>
    private static IEnumerable<IReadOnlyIndex> GetSecondaryIndexes(IReadOnlyEntityType rootEntityType)
        => rootEntityType
            .GetDerivedTypesInclusive()
            .OrderBy(static entityType => entityType.Name, StringComparer.Ordinal)
            .SelectMany(static entityType => entityType.GetDeclaredIndexes())
            .Where(static index => index.GetSecondaryIndexKind() is not null)
            .OrderBy(static index => index.GetSecondaryIndexName() ?? index.Name ?? string.Empty, StringComparer.Ordinal)
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
        IReadOnlyEntityType entityType,
        IReadOnlyIndex index,
        string indexName)
    {
        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' must define a DynamoDB partition key before local secondary index '{indexName}' can be used.");

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

    /// <summary>Ensures shared-table root entity types expose equivalent runtime sources for analysis.</summary>
    private static void ValidateSharedTableSources(
        string tableName,
        IReadOnlyDictionary<string, IReadOnlyList<DynamoIndexDescriptor>> sourcesByEntityType)
    {
        string? baselineEntityTypeName = null;
        IReadOnlyList<DynamoIndexDescriptor>? baselineSources = null;

        foreach (var pair in sourcesByEntityType)
        {
            if (baselineSources is null)
            {
                baselineEntityTypeName = pair.Key;
                baselineSources = pair.Value;
                continue;
            }

            if (HaveEquivalentSourceSignatures(baselineSources, pair.Value))
                continue;

            throw new InvalidOperationException(
                $"Entity types '{baselineEntityTypeName}' and '{pair.Key}' map to DynamoDB table '{tableName}' but expose inconsistent secondary-index metadata.");
        }
    }

    /// <summary>
    ///     Determines whether two source lists describe the same access paths by metadata shape.
    /// </summary>
    /// <remarks>
    ///     Comparison is positional. Both lists must be in the same order, which is guaranteed
    ///     because <see cref="BuildSourceDescriptors"/> always places the base-table descriptor
    ///     first and then appends secondary indexes in the order returned by
    ///     <see cref="GetSecondaryIndexes"/>. Any future change to that ordering must be reflected
    ///     here to keep shared-table consistency validation correct.
    /// </remarks>
    private static bool HaveEquivalentSourceSignatures(
        IReadOnlyList<DynamoIndexDescriptor> left,
        IReadOnlyList<DynamoIndexDescriptor> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (!HasEquivalentSourceSignature(left[index], right[index]))
                return false;
        }

        return true;
    }

    /// <summary>Determines whether two source descriptors are equivalent for shared-table analysis.</summary>
    private static bool HasEquivalentSourceSignature(DynamoIndexDescriptor left, DynamoIndexDescriptor right)
        => left.IndexName == right.IndexName
            && left.Kind == right.Kind
            && left.ProjectionType == right.ProjectionType
            && left.PartitionKeyProperty.GetAttributeName() == right.PartitionKeyProperty.GetAttributeName()
            && left.SortKeyProperty?.GetAttributeName() == right.SortKeyProperty?.GetAttributeName()
            && GetKeyTypeCategory(GetEffectiveProviderClrType(left.PartitionKeyProperty))
                == GetKeyTypeCategory(GetEffectiveProviderClrType(right.PartitionKeyProperty))
            && GetSortKeyTypeCategory(left.SortKeyProperty) == GetSortKeyTypeCategory(right.SortKeyProperty);

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
        => property is null ? DynamoKeyTypeCategory.Unsupported : GetKeyTypeCategory(GetEffectiveProviderClrType(property));

    /// <summary>Represents supported DynamoDB key type categories for runtime consistency checks.</summary>
    private enum DynamoKeyTypeCategory
    {
        Unsupported,
        String,
        Number,
        Binary,
    }
}
