using Microsoft.EntityFrameworkCore.DynamoDB;

namespace EntityFrameworkCore.DynamoDb.Tests.Diagnostics;

/// <summary>Tests DynamoDB-specific provider logger category names.</summary>
public class DynamoLoggerCategoryTests
{
    private const string CapacityName = "Microsoft.EntityFrameworkCore.DynamoDB.Capacity";

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Capacity_Name_IsExpected()
        => DbLoggerCategory.Capacity.Name.Should().Be(CapacityName);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Capacity_ToString_IsExpected()
        => new DbLoggerCategory.Capacity().ToString().Should().Be(CapacityName);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Capacity_ImplicitStringConversion_IsExpected()
    {
        DbLoggerCategory.Capacity category = new();

        string name = category;

        name.Should().Be(CapacityName);
    }
}
