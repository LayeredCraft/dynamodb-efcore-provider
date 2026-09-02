using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.HealthChecks.Tests;

public sealed class DynamoDbHealthChecksTests
{
    [Fact(Timeout = 30_000)]
    public async Task AddDbContextCheck_ReportsHealthy_WhenDynamoDbCanConnect()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ListTablesAsync(
                Arg.Is<ListTablesRequest>(request => request.Limit == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ListTablesResponse()));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HealthCheckContext>(options
            => options.UseDynamo(dynamo => dynamo.DynamoDbClient(client)));
        services.AddHealthChecks().AddDbContextCheck<HealthCheckContext>();
        using var serviceProvider = services.BuildServiceProvider();

        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        report.Status.Should().Be(HealthStatus.Healthy);
        report
            .Entries
            .Should()
            .ContainKey(nameof(HealthCheckContext))
            .WhoseValue
            .Status
            .Should()
            .Be(HealthStatus.Healthy);
        await client
            .Received(1)
            .ListTablesAsync(
                Arg.Is<ListTablesRequest>(request => request.Limit == 1),
                Arg.Any<CancellationToken>());
    }

    private sealed class HealthCheckContext(DbContextOptions<HealthCheckContext> options)
        : DbContext(options);
}
