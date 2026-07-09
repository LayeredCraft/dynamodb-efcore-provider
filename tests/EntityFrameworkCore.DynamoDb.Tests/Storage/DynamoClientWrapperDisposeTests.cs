using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class DynamoClientWrapperDisposeTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Disposing_context_does_not_dispose_user_supplied_client()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using (var context = new DisposeContext(client))
        {
            context.GetService<IDynamoClientWrapper>().Client.Should().BeSameAs(client);
        }

        client.DidNotReceive().Dispose();
    }

    private sealed class DisposeContext(IAmazonDynamoDB client) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }
}
