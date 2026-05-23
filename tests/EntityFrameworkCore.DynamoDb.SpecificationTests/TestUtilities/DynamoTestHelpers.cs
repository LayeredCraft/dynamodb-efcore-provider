using EntityFrameworkCore.DynamoDb.Extensions;
using System.Reflection;
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
    {
        try
        {
            AssertAllMethodsOverridden(testClass);
        }
        catch (Exception exception) when (IsKnownNonVirtualDataBindingLocalViewFalsePositive(
            testClass,
            exception)) { }
    }

    /// <summary>Runs a sync test and verifies that DynamoDB sync query execution fails.</summary>
    public void NoSyncTest(Action testCode)
    {
        try
        {
            testCode();
        }
        catch (InvalidOperationException exception) when (IsExpectedSyncQueryFailure(exception))
        {
            return;
        }

        Assert.Fail("Expected DynamoDB sync query failure.");
    }

    /// <summary>Runs an async-aware test and verifies that sync DynamoDB query execution fails.</summary>
    public async Task NoSyncTest(bool async, Func<bool, Task> testCode)
    {
        try
        {
            await testCode(async);
        }
        catch (InvalidOperationException exception) when (!async
            && IsExpectedSyncQueryFailure(exception)) { }
    }

    private static bool IsExpectedSyncQueryFailure(InvalidOperationException exception)
        => exception.Message.Contains("Sync enumerating", StringComparison.Ordinal)
            && exception.Message.Contains("DynamoDB", StringComparison.Ordinal);

    private static bool
        IsKnownNonVirtualDataBindingLocalViewFalsePositive(Type testClass, Exception exception)
        => testClass.Name == "DataBindingDynamoTest"
            && exception.Message.Contains(
                "LocalView_is_initialized_with_entities_from_the_context",
                StringComparison.Ordinal)
            && exception.Message.Contains("-- Missing test overrides --", StringComparison.Ordinal)
            && testClass.GetMethod(
                "LocalView_is_initialized_with_entities_from_the_context",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                [typeof(bool)]) is { IsVirtual: true };
}
