using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoTransactionTargetIdentityFactory(
    DynamoEntityItemSerializerSource serializerSource)
{
    internal TransactionTargetItem Create(IUpdateEntry entry, string tableName)
    {
        var entityType = entry.EntityType;

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var partitionKeyValue = SerializeIdentityValue(entry, partitionKeyProperty);

        var sortKeyProperty = entityType.GetSortKeyProperty();
        var sortKeyValue = sortKeyProperty is null
            ? string.Empty
            : SerializeIdentityValue(entry, sortKeyProperty);

        return new TransactionTargetItem(tableName, partitionKeyValue, sortKeyValue);
    }

    private string SerializeIdentityValue(IUpdateEntry entry, IProperty keyProperty)
    {
        var value = serializerSource.GetOrBuildOriginalValueSerializer(keyProperty)(entry);
        return SerializeKeyAttributeValue(value, entry.EntityType, keyProperty);
    }

    private static string SerializeKeyAttributeValue(
        AttributeValue value,
        IEntityType entityType,
        IProperty keyProperty)
    {
        if (value.S is not null)
            return "S:" + value.S;
        if (value.N is not null)
            return "N:" + value.N;
        if (value.B is not null)
            return "B:" + Convert.ToBase64String(value.B.ToArray());

        throw new InvalidOperationException(
            $"Key property '{entityType.DisplayName()}.{keyProperty.Name}' produced "
            + "an unsupported DynamoDB key shape for transaction target identity "
            + "comparison. Only S, N, and B are supported.");
    }
}
