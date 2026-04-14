using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed record CompiledWriteOperation(
    IUpdateEntry Entry,
    EntityState EntityState,
    string TableName,
    string Statement,
    List<AttributeValue> Parameters,
    TransactionTargetItem TargetItem);

internal sealed record TransactionTargetItem(string TableName, string PartitionKey, string SortKey);

internal sealed record DynamoWritePlan(
    IList<IUpdateEntry> Entries,
    IReadOnlyList<IUpdateEntry> RootEntries,
    IReadOnlyList<CompiledWriteOperation> Operations);
