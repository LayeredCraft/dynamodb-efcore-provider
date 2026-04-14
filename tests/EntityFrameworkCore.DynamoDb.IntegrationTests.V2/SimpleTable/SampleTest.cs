using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.Infra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

public class SampleTest(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)

{
    [Fact]
    public async Task ValidateThatStuffWorks() => await Task.CompletedTask;
}
