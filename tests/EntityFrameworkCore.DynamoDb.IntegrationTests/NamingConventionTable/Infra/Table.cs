using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Manages DynamoDB table creation and seed data for naming convention integration tests. The
///     table key uses the snake_case attribute name <c>pk</c> because the EF provider will derive the
///     partition key attribute name from <c>GetAttributeName()</c>, which applies the configured
///     naming convention.
/// </summary>
public static class NamingConventionItemTable
{
    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = "NamingConventionItems";

    /// <summary>Creates the DynamoDB table and seeds it with test data.</summary>
    public static async Task CreateTable(
        IAmazonDynamoDB dynamoDb,
        CancellationToken cancellationToken)
    {
        await dynamoDb.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                // Partition key uses snake_case attribute name — matches the convention applied
                // to the CLR property "Pk" by the EF provider.
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

        await dynamoDb.TransactWriteItemsAsync(
            new TransactWriteItemsRequest
            {
                TransactItems =
                    NamingConventionItems
                        .AttributeValues
                        .Select(a => new TransactWriteItem
                        {
                            Put = new Put { TableName = TableName, Item = a },
                        })
                        .ToList(),
            },
            cancellationToken);
    }
}
