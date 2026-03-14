using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Represents the canonical runtime view of DynamoDB table metadata derived from the EF model.</summary>
internal sealed record DynamoRuntimeTableModel(
    IReadOnlyDictionary<string, DynamoTableDescriptor> Tables);

/// <summary>Represents one physical DynamoDB table group and its source mappings.</summary>
internal sealed record DynamoTableDescriptor(
    string TableName,
    IReadOnlyList<IReadOnlyEntityType> RootEntityTypes,
    IReadOnlyDictionary<string, IReadOnlyList<DynamoIndexDescriptor>> SourcesByRootEntityTypeName,
    IReadOnlyDictionary<string, IReadOnlyList<DynamoIndexDescriptor>> SourcesByQueryEntityTypeName);

/// <summary>Represents one base-table or secondary-index source available to DynamoDB query planning.</summary>
internal sealed record DynamoIndexDescriptor(
    string? IndexName,
    DynamoIndexSourceKind Kind,
    IReadOnlyIndex? ModelIndex,
    IReadOnlyProperty PartitionKeyProperty,
    IReadOnlyProperty? SortKeyProperty,
    DynamoSecondaryIndexProjectionType ProjectionType);

/// <summary>Identifies the runtime source kind available to DynamoDB query planning.</summary>
internal enum DynamoIndexSourceKind
{
    Table,
    GlobalSecondaryIndex,
    LocalSecondaryIndex,
}
