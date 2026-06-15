using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Resolved DynamoDB table-key metadata for a root entity type.</summary>
/// <param name="PrimaryKeyConfigurationSource">
/// Configuration source for <paramref name="PrimaryKey"/>; non-null if and only if <paramref name="PrimaryKey"/> is non-null.
/// </param>
internal sealed record DynamoResolvedTableKey(
    IConventionProperty? PartitionKey,
    IConventionProperty? SortKey,
    IConventionKey? PrimaryKey,
    ConfigurationSource? PrimaryKeyConfigurationSource,
    ConfigurationSource? PartitionKeyConfigurationSource,
    ConfigurationSource? SortKeyConfigurationSource);
