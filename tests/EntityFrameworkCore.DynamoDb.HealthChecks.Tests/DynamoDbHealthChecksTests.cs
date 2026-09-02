using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
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

    [Fact(Timeout = 30_000)]
    public async Task AddDbContextCheck_ReportsUnhealthy_WhenDynamoDbCannotConnect()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ListTablesAsync(Arg.Any<ListTablesRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ListTablesResponse>>(_
                => throw new AmazonDynamoDBException("Unavailable"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HealthCheckContext>(options
            => options.UseDynamo(dynamo => dynamo.DynamoDbClient(client)));
        services.AddHealthChecks().AddDbContextCheck<HealthCheckContext>();
        using var serviceProvider = services.BuildServiceProvider();

        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        report.Status.Should().Be(HealthStatus.Unhealthy);
        report
            .Entries
            .Should()
            .ContainKey(nameof(HealthCheckContext))
            .WhoseValue
            .Status
            .Should()
            .Be(HealthStatus.Unhealthy);
    }

    [Fact(Timeout = 30_000)]
    public void ToTable_CanBeExplicitlyInvoked_WhenHealthCheckPackageIsInstalled()
    {
        var options = new DbContextOptionsBuilder<MappedHealthCheckContext>().UseDynamo().Options;

        using var context = new MappedHealthCheckContext(options);

        var entityType = context.Model.FindEntityType(typeof(HealthCheckItem))!;

        entityType.FindAnnotation(DynamoAnnotationNames.TableName)!
            .Value
            .Should()
            .Be("health-checks");
    }

    private sealed class HealthCheckContext(DbContextOptions<HealthCheckContext> options)
        : DbContext(options);

    private sealed class MappedHealthCheckContext(
        DbContextOptions<MappedHealthCheckContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => DynamoEntityTypeBuilderExtensions.ToTable(
                modelBuilder.Entity<HealthCheckItem>(),
                "health-checks");
    }

    private sealed class HealthCheckItem
    {
        public string Id { get; init; } = null!;
    }
}
