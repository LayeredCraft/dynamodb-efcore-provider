using System.Reflection;
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Disposing_provider_owned_client_marks_wrapper_disposed()
    {
        var oldAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var oldSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");

        try
        {
            await using var context = new ProviderOwnedDisposeContext();
            var wrapper = (DynamoClientWrapper)context.GetService<IDynamoClientWrapper>();

            wrapper.Client.Should().BeOfType<AmazonDynamoDBClient>();
            GetPrivateBool(wrapper, "_ownsClient").Should().BeTrue();

            wrapper.Dispose();
            wrapper.Dispose();

            GetPrivateBool(wrapper, "_disposed").Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", oldAccessKey);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", oldSecretKey);
        }
    }

    private static bool GetPrivateBool(DynamoClientWrapper wrapper, string fieldName)
        => (bool)typeof(DynamoClientWrapper).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wrapper)!;

    private sealed class DisposeContext(IAmazonDynamoDB client) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    private sealed class ProviderOwnedDisposeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo(options => options.DynamoDbClientConfig(
                    new AmazonDynamoDBConfig
                    {
                        ServiceURL = "http://localhost:8000", AuthenticationRegion = "us-east-1"
                    }))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }
}
