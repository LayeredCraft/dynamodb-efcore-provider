using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;

public class SimpleTest(DynamoContainerFixture fixture) : SharedTableTestFixture(fixture)

{
    [Fact]
    public async Task ValidateThatStuffWorks() => await Task.CompletedTask;
}
