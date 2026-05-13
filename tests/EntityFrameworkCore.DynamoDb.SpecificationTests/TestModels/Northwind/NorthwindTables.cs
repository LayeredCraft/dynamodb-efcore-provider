using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

public static class NorthwindTables
{
    public const string Customers = "Northwind_Customers";
    public const string Employees = "Northwind_Employees";
    public const string Orders = "Northwind_Orders";
    public const string OrderDetails = "Northwind_OrderDetails";
    public const string Products = "Northwind_Products";

    public static async Task RecreateAndSeedAsync(
        IAmazonDynamoDB client,
        CancellationToken cancellationToken)
    {
        foreach (var table in new[] { Customers, Employees, Orders, OrderDetails, Products })
        {
            await DeleteIfExistsAsync(client, table, cancellationToken);
        }

        await CreatePkTableAsync(
            client,
            Customers,
            "customerID",
            ScalarAttributeType.S,
            cancellationToken);
        await CreatePkTableAsync(
            client,
            Employees,
            "employeeID",
            ScalarAttributeType.N,
            cancellationToken);
        await CreatePkTableAsync(
            client,
            Orders,
            "orderID",
            ScalarAttributeType.N,
            cancellationToken);
        await CreatePkTableAsync(
            client,
            Products,
            "productID",
            ScalarAttributeType.N,
            cancellationToken);
        await CreatePkSkTableAsync(client, OrderDetails, "orderID", "productID", cancellationToken);

        var data = NorthwindData.Instance;
        await client.SeedItemsAsync(
            Customers,
            data.Customers.Select(NorthwindMappers.ToItem),
            cancellationToken);
        await client.SeedItemsAsync(
            Employees,
            data.Employees.Select(NorthwindMappers.ToItem),
            cancellationToken);
        await client.SeedItemsAsync(
            Orders,
            data.Orders.Select(NorthwindMappers.ToItem),
            cancellationToken);
        await client.SeedItemsAsync(
            OrderDetails,
            data.OrderDetails.Select(NorthwindMappers.ToItem),
            cancellationToken);
        await client.SeedItemsAsync(
            Products,
            data.Products.Select(NorthwindMappers.ToItem),
            cancellationToken);
    }

    private static async Task DeleteIfExistsAsync(
        IAmazonDynamoDB client,
        string tableName,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteTableAsync(tableName, cancellationToken);
            await WaitForDeletedAsync(client, tableName, cancellationToken);
        }
        catch (ResourceNotFoundException) { }
    }

    private static Task CreatePkTableAsync(
        IAmazonDynamoDB client,
        string tableName,
        string pk,
        ScalarAttributeType pkType,
        CancellationToken cancellationToken)
        => client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions = [new AttributeDefinition(pk, pkType)],
                KeySchema = [new KeySchemaElement(pk, KeyType.HASH)],
                BillingMode = BillingMode.PAY_PER_REQUEST
            },
            cancellationToken);

    private static Task CreatePkSkTableAsync(
        IAmazonDynamoDB client,
        string tableName,
        string pk,
        string sk,
        CancellationToken cancellationToken)
        => client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition(pk, ScalarAttributeType.N),
                    new AttributeDefinition(sk, ScalarAttributeType.N)
                ],
                KeySchema =
                [
                    new KeySchemaElement(pk, KeyType.HASH),
                    new KeySchemaElement(sk, KeyType.RANGE)
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST
            },
            cancellationToken);

    private static async Task WaitForDeletedAsync(
        IAmazonDynamoDB client,
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await client.DescribeTableAsync(tableName, cancellationToken);
                await Task.Delay(50, cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }
        }
    }
}
