using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ConstructorMaterializationTable;

public static class ConstructorBlogTable
{
    public const string TableName = "ConstructorBlogs";

    public static Task CreateTable(IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken)
        => dynamoDb.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "pk", AttributeType = ScalarAttributeType.S
                    }
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH }
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST
            },
            cancellationToken);
}
