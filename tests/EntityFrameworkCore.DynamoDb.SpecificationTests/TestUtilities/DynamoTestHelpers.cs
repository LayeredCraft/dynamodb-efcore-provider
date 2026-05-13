using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Provider test helpers for DynamoDB specification tests.</summary>
public class DynamoTestHelpers : TestHelpers
{
    protected DynamoTestHelpers() { }

    public static DynamoTestHelpers Instance { get; } = new();

    public override ModelAsserter ModelAsserter => ModelAsserter.Instance;

    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkDynamo();

    public override DbContextOptionsBuilder UseProviderOptions(
        DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo();

    /// <summary>Asserts that provider specification tests explicitly override inherited test methods.</summary>
    public static void AssertAllTestMethodsOverridden(Type testClass)
        => AssertAllMethodsOverridden(testClass);

    /// <summary>Runs a sync test and verifies that DynamoDB sync query execution fails.</summary>
    public void NoSyncTest(Action testCode)
    {
        try
        {
            testCode();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(
                "Sync enumerating",
                StringComparison.Ordinal)
            && exception.Message.Contains("DynamoDB", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Fail("Expected DynamoDB sync query failure.");
    }
}
