using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Builds runtime DynamoDB table metadata after model validation completes.</summary>
public sealed class DynamoModelRuntimeInitializer(
    ModelRuntimeInitializerDependencies dependencies)
    : ModelRuntimeInitializer(dependencies)
{
    /// <summary>Attaches the canonical runtime table model to the finalized runtime model.</summary>
    public override IModel Initialize(
        IModel model,
        bool designTime = true,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation>? validationLogger = null)
    {
        var initializedModel = base.Initialize(model, designTime, validationLogger);

        var runtimeModel = designTime
            ? (IModel)initializedModel.FindRuntimeAnnotationValue(CoreAnnotationNames.ReadOnlyModel)!
            : initializedModel;

        runtimeModel.GetOrAddRuntimeAnnotationValue(
            DynamoAnnotationNames.RuntimeTableModel,
            static currentModel => BuildRuntimeTableModel(currentModel),
            runtimeModel);

        return initializedModel;
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

        foreach (var index in entityType.GetIndexes())
        {
            var secondaryIndexKind = index.GetSecondaryIndexKind();
            if (secondaryIndexKind is null)
                continue;

            var indexName = index.GetSecondaryIndexName()
                ?? throw new InvalidOperationException(
                    $"Secondary index '{index.Name ?? "<unnamed>"}' on entity type '{entityType.DisplayName()}' is missing a DynamoDB index name.");

            sources.Add(secondaryIndexKind switch
            {
                DynamoSecondaryIndexKind.Global => BuildGlobalSecondaryIndexDescriptor(index, indexName),
                DynamoSecondaryIndexKind.Local => BuildLocalSecondaryIndexDescriptor(entityType, index, indexName),
                _ => throw new InvalidOperationException(
                    $"Secondary index '{indexName}' on entity type '{entityType.DisplayName()}' has an unsupported kind '{secondaryIndexKind}'."),
            });
        }

        return sources;
    }

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
                $"Local secondary index '{indexName}' on entity type '{entityType.DisplayName()}' must define exactly one alternate sort-key property.");
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

    /// <summary>Determines whether two source lists describe the same access paths by metadata shape.</summary>
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
            && left.SortKeyProperty?.GetAttributeName() == right.SortKeyProperty?.GetAttributeName();
}
