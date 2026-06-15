using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Resolved DynamoDB table-key metadata for a root entity type.</summary>
/// <param name="PartitionKey">Resolved DynamoDB partition-key property, if configured.</param>
/// <param name="SortKey">Resolved DynamoDB sort-key property, if configured.</param>
/// <param name="PrimaryKey">Resolved EF primary key backing DynamoDB key metadata, if available.</param>
/// <param name="PrimaryKeyConfigurationSource">
/// Configuration source for <paramref name="PrimaryKey"/>; non-null if and only if <paramref name="PrimaryKey"/> is non-null.
/// </param>
/// <param name="PartitionKeyConfigurationSource">Configuration source for <paramref name="PartitionKey"/>.</param>
/// <param name="SortKeyConfigurationSource">Configuration source for <paramref name="SortKey"/>.</param>
internal sealed record DynamoResolvedTableKey(
    IConventionProperty? PartitionKey,
    IConventionProperty? SortKey,
    IConventionKey? PrimaryKey,
    ConfigurationSource? PrimaryKeyConfigurationSource,
    ConfigurationSource? PartitionKeyConfigurationSource,
    ConfigurationSource? SortKeyConfigurationSource);
