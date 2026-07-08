using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class LoggingDynamoTest : LoggingTestBase
{
    [ConditionalFact]
    public void Check_all_tests_overridden()
    {
        var testClass = typeof(LoggingDynamoTest);
        // LoggingTestBase exposes non-virtual facts; shadow those explicitly below.
        var inheritedOverridableTests = testClass
            .GetRuntimeMethods()
            .Where(method => method.DeclaringType != testClass
                && method.IsVirtual
                && !method.IsFinal
                && (Attribute.IsDefined(method, typeof(ConditionalFactAttribute))
                    || Attribute.IsDefined(method, typeof(ConditionalTheoryAttribute))))
            .Select(method => method.Name);

        Assert.Empty(inheritedOverridableTests);
    }

    [ConditionalFact]
    public new void Logs_context_initialization_default_options()
        => base.Logs_context_initialization_default_options();

    [ConditionalFact]
    public new void Logs_context_initialization_no_tracking()
        => base.Logs_context_initialization_no_tracking();

    [ConditionalFact]
    public new void Logs_context_initialization_sensitive_data_logging()
        => base.Logs_context_initialization_sensitive_data_logging();

    [ConditionalFact(Skip = SkipReason.PartitionKeyRequiredOnAllEntities)]
    public override void InvalidIncludePathError_throws_by_default()
        => base.InvalidIncludePathError_throws_by_default();

    protected override TestLogger CreateTestLogger() => new TestLogger<TestLoggingDefinitions>();

    protected override DbContextOptionsBuilder CreateOptionsBuilder(IServiceCollection services)
    {
        var serviceProvider = services.AddEntityFrameworkDynamo().BuildServiceProvider(true);

        return new DbContextOptionsBuilder()
            .UseDynamo()
            .UseInternalServiceProvider(serviceProvider)
            .ConfigureWarnings(w => w
                .Default(WarningBehavior.Throw)
                .Log(CoreEventId.SensitiveDataLoggingEnabledWarning));
    }

    protected override string DefaultOptions
        => new DbContextOptionsBuilder()
            .UseDynamo()
            .Options
            .FindExtension<DynamoDbOptionsExtension>()!.Info.LogFragment;

    protected override string ProviderName => "EntityFrameworkCore.DynamoDb";

    protected override string ProviderVersion
        => typeof(DynamoDbContextOptionsBuilder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
