using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class LoggingDynamoTest : LoggingTestBase
{
    [ConditionalFact(Skip = SkipReason.PartitionKeyRequiredOnAllEntities)]
    public override void InvalidIncludePathError_throws_by_default() { }

    protected override string ActualMessage(Func<IServiceCollection, DbContextOptionsBuilder> optionsActions)
    {
        var options = optionsActions(new ServiceCollection()).Options;
        var coreOptions = options.FindExtension<CoreOptionsExtension>();

        var optionsFragment = DefaultOptions;
        if (coreOptions?.QueryTrackingBehavior == QueryTrackingBehavior.NoTracking)
        {
            optionsFragment = "NoTracking " + optionsFragment;
        }

        if (coreOptions?.IsSensitiveDataLoggingEnabled == true)
        {
            optionsFragment = "SensitiveDataLoggingEnabled " + optionsFragment;
        }

        return ExpectedMessage(optionsFragment);
    }

    protected override TestLogger CreateTestLogger()
        => new TestLogger<TestLoggingDefinitions>();

    protected override DbContextOptionsBuilder CreateOptionsBuilder(IServiceCollection services)
    {
        var serviceProvider = DynamoTestStoreFactory.Instance
            .AddProviderServices(services)
            .BuildServiceProvider(validateScopes: true);

        return new DbContextOptionsBuilder()
            .UseDynamo()
            .UseInternalServiceProvider(serviceProvider)
            .ConfigureWarnings(w => w.Default(WarningBehavior.Throw));
    }

    protected override string ProviderName => "EntityFrameworkCore.DynamoDb";

    protected override string ProviderVersion
        => typeof(DynamoDbContextOptionsBuilder)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
}
