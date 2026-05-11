using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
