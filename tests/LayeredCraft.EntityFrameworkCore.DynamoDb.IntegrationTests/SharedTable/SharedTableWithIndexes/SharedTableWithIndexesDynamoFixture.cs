using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

/// <summary>
/// xUnit class fixture that manages a DynamoDB Local container for the shared-table-with-indexes
/// integration tests. Each test class that uses this fixture shares a single container instance
/// while getting per-test table reset via <see cref="SharedTableWithIndexesTestBase"/>.
/// </summary>
public sealed class SharedTableWithIndexesDynamoFixture : DynamoFixture;
