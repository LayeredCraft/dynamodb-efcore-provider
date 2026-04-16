using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Creates and seeds the snake_case entity table. The partition key attribute name is
///     <c>pk</c> — the snake_case transform of the CLR property <c>Pk</c>.
/// </summary>
public static class SnakeCaseItemTable
{
    /// <summary>DynamoDB table name.</summary>
    public const string TableName = "NamingConventionSnakeCase";

    /// <summary>Creates the DynamoDB table and seeds it with test data.</summary>
    public static async Task CreateTable(
        IAmazonDynamoDB client,
        CancellationToken cancellationToken)
    {
        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "pk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await client.TransactWriteItemsAsync(
            new TransactWriteItemsRequest
            {
                TransactItems = NamingConventionData
                    .SnakeCaseAttributeValues
                    .Select(a => new TransactWriteItem
                    {
                        Put = new Put { TableName = TableName, Item = a },
                    })
                    .ToList(),
            },
            cancellationToken);
    }
}

/// <summary>
///     Creates and seeds the kebab-case entity table. The partition key attribute name is
///     <c>pk</c> — the kebab-case transform of the CLR property <c>Pk</c>.
/// </summary>
public static class KebabCaseItemTable
{
    /// <summary>DynamoDB table name.</summary>
    public const string TableName = "NamingConventionKebabCase";

    /// <summary>Creates the DynamoDB table and seeds it with test data.</summary>
    public static async Task CreateTable(
        IAmazonDynamoDB client,
        CancellationToken cancellationToken)
    {
        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "pk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await client.TransactWriteItemsAsync(
            new TransactWriteItemsRequest
            {
                TransactItems = NamingConventionData
                    .KebabCaseAttributeValues
                    .Select(a => new TransactWriteItem
                    {
                        Put = new Put { TableName = TableName, Item = a },
                    })
                    .ToList(),
            },
            cancellationToken);
    }
}
