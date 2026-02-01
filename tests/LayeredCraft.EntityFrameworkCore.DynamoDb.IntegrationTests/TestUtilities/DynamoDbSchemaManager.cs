using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public static class DynamoDbSchemaManager
{
    public static async Task DeleteAllTablesAsync(
        IAmazonDynamoDB client,
        CancellationToken cancellationToken)
    {
        var tableNames = await ListAllTablesAsync(client, cancellationToken);
        if (tableNames.Count == 0)
        {
            return;
        }

        foreach (var tableName in tableNames)
        {
            try
            {
                await client.DeleteTableAsync(tableName, cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                // Table disappeared between ListTables and DeleteTable.
            }
        }

        foreach (var tableName in tableNames)
        {
            await WaitForTableDeletedAsync(client, tableName, cancellationToken);
        }
    }

    public static async Task WaitForTableActiveAsync(
        IAmazonDynamoDB client,
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var response = await client.DescribeTableAsync(tableName, cancellationToken);
            if (response.Table.TableStatus == TableStatus.ACTIVE)
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    public static async Task WaitForTableDeletedAsync(
        IAmazonDynamoDB client,
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                _ = await client.DescribeTableAsync(tableName, cancellationToken);
                await Task.Delay(50, cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }
        }
    }

    private static async Task<List<string>> ListAllTablesAsync(
        IAmazonDynamoDB client,
        CancellationToken cancellationToken)
    {
        ListTablesResponse response;
        string? start = null;

        var tables = new List<string>();
        do
        {
            response = await client.ListTablesAsync(
                new ListTablesRequest { ExclusiveStartTableName = start },
                cancellationToken);

            tables.AddRange(response.TableNames);
            start = response.LastEvaluatedTableName;
        } while (start is not null);

        return tables;
    }
}
