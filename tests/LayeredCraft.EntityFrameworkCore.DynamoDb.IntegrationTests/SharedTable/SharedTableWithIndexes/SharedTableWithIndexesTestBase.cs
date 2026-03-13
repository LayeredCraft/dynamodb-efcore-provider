using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

/// <summary>
/// Abstract base for shared-table-with-indexes integration tests. Creates the
/// <c>work-orders-indexed-table</c> with full GSI (<c>ByPriority</c>) and LSI (<c>ByStatus</c>)
/// configuration, seeds deterministic work-order data, and resets state between each test.
/// </summary>
public abstract class SharedTableWithIndexesTestBase(SharedTableWithIndexesDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<SharedTableWithIndexesDynamoFixture, SharedTableWithIndexesDbContext>(
        fixture)
{
    /// <inheritdoc />
    /// <remarks>
    /// Overrides the default options to enable
    /// <c>DynamoAutomaticIndexSelectionMode.Conservative</c> so the analyzer may rewrite
    /// query sources during these tests.
    /// </remarks>
    protected override DbContextOptions<SharedTableWithIndexesDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var baseOptions = base.CreateOptions(loggerFactory);
        var builder = new DbContextOptionsBuilder<SharedTableWithIndexesDbContext>(baseOptions);
        builder.UseDynamo(opt =>
            opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    /// <inheritdoc />
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SharedTableWithIndexesDbContext.TableName,

                // Only attributes that appear in at least one key schema need to be declared.
                // The "$type" discriminator is NOT declared — it is never an index key.
                AttributeDefinitions =
                [
                    // Base table keys (also LSI shared PK).
                    new AttributeDefinition
                    {
                        AttributeName = "Pk",
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Sk",
                        AttributeType = ScalarAttributeType.S,
                    },

                    // LSI "ByStatus" sort key.
                    new AttributeDefinition
                    {
                        AttributeName = "Status",
                        AttributeType = ScalarAttributeType.S,
                    },

                    // GSI "ByPriority" partition key — numeric type.
                    new AttributeDefinition
                    {
                        AttributeName = "Priority",
                        AttributeType = ScalarAttributeType.N,
                    },
                ],

                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = "Sk", KeyType = KeyType.RANGE },
                ],

                GlobalSecondaryIndexes =
                [
                    // GSI: all priority work orders at a given priority level, cross-tenant.
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByPriority",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = "Priority",
                                KeyType = KeyType.HASH,
                            },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],

                LocalSecondaryIndexes =
                [
                    // LSI: work orders for a given tenant key, ordered by status value.
                    new LocalSecondaryIndex
                    {
                        IndexName = "ByStatus",
                        KeySchema =
                        [
                            new KeySchemaElement { AttributeName = "Pk",     KeyType = KeyType.HASH  },
                            new KeySchemaElement { AttributeName = "Status",  KeyType = KeyType.RANGE },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],

                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await DynamoDbSchemaManager.WaitForTableActiveAsync(
            Client,
            SharedTableWithIndexesDbContext.TableName,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        // WorkOrderEntity is abstract, so we can't use a source-generated DynamoMapper.
        // Build AttributeValue maps directly to avoid coupling to materialization logic.
        var writeRequests = SharedTableWithIndexesItems.AttributeValues
            .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
            .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(batch, cancellationToken);
        }
    }

    /// <summary>Writes a single DynamoDB batch and retries any unprocessed items until completion.</summary>
    private async Task BatchWriteWithRetriesAsync(
        List<WriteRequest> writeRequests,
        CancellationToken cancellationToken)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [SharedTableWithIndexesDbContext.TableName] = writeRequests,
            },
        };

        // Retry until all items are written (handles unprocessed-items throttling).
        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
