using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Resolved DynamoDB table-key metadata for a root entity type.</summary>
internal sealed record DynamoResolvedTableKey(
    IConventionProperty? PartitionKey,
    IConventionProperty? SortKey,
    IConventionKey? PrimaryKey,
    ConfigurationSource? PrimaryKeyConfigurationSource,
    ConfigurationSource? PartitionKeyConfigurationSource,
    ConfigurationSource? SortKeyConfigurationSource);
