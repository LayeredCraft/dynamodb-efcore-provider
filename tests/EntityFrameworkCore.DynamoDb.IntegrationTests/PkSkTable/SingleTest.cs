using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class SingleTest(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    // TODO: delete before merging
    [Fact]
    public async Task DeleteBeforeMerging()
    {
        var result = await Client.ExecuteStatementAsync(
            request: new ExecuteStatementRequest
            {
                Statement = """
                            SELECT *
                            FROM "PkSkItems"
                            WHERE "pk" = 'P#1' AND "sk" = '0002'
                            """,
                Limit = 1
            },
            CancellationToken);

        Assert.Fail("This is a dummy test.");
    }
}
