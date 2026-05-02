using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>Unit tests for scan-query behavior options.</summary>
public class DynamoScanQueryBehaviorOptionsTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DefaultScanQueryBehavior_IsThrow()
    {
        var extension = new DynamoDbOptionsExtension();

        extension.ScanQueryBehavior.Should().Be(DynamoScanQueryBehavior.Throw);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ScanQueryBehavior_IsStoredOnOptionsExtension()
    {
        var options = new DbContextOptionsBuilder().UseDynamo(dynamo
                => dynamo.ScanQueryBehavior(DynamoScanQueryBehavior.Warn))
            .Options;

        options
            .FindExtension<DynamoDbOptionsExtension>()
            .Should()
            .NotBeNull()
            .And
            .Match<DynamoDbOptionsExtension>(e
                => e.ScanQueryBehavior == DynamoScanQueryBehavior.Warn);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ScanQueryBehavior_AffectsServiceProviderHashAndEquality()
    {
        var throwExtension = new DynamoDbOptionsExtension();
        var warnExtension = throwExtension.WithScanQueryBehavior(DynamoScanQueryBehavior.Warn);

        throwExtension
            .Info
            .GetServiceProviderHashCode()
            .Should()
            .NotBe(warnExtension.Info.GetServiceProviderHashCode());
        throwExtension.Info.ShouldUseSameServiceProvider(warnExtension.Info).Should().BeFalse();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ScanQueryBehavior_AppearsInLogFragment()
    {
        var extension =
            new DynamoDbOptionsExtension().WithScanQueryBehavior(DynamoScanQueryBehavior.Allow);

        extension.Info.LogFragment.Should().Contain("ScanQueryBehavior=Allow");
    }
}
