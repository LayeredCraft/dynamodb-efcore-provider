using EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions;

public class SelectTests(DynamoContainerFixture fixture)
    : NamingConventionsTableTestFixture(fixture)
{
    [Fact]
    public async Task ToListAsync_MaterializesNamingConventionsAndCollections()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        var expected = NamingConventionsItems.Items.ToList();
    }
}
