using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

public class ModelConfigurationTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    [Fact]
    public void PartitionKey_And_SortKey_AreConfiguredCorrectly()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;

        entityType.GetPartitionKeyPropertyName().Should().Be(nameof(OrderItem.CustomerId));
        entityType.GetSortKeyPropertyName().Should().Be(nameof(OrderItem.OrderId));
        entityType.FindPrimaryKey()!
            .Properties
            .Select(p => p.Name)
            .Should()
            .Equal(nameof(OrderItem.CustomerId), nameof(OrderItem.OrderId));
    }

    [Fact]
    public void FourSecondaryIndexes_AreRegisteredOnEntityType()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;

        entityType
            .GetIndexes()
            .Where(i => i.GetSecondaryIndexName() is not null)
            .Should()
            .HaveCount(4);
    }

    [Fact]
    public void GsiByStatus_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByStatus");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index
            .Properties
            .Select(p => p.Name)
            .Should()
            .Equal(nameof(OrderItem.Status), nameof(OrderItem.CreatedAt));
    }

    [Fact]
    public void GsiByRegion_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByRegion");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index
            .Properties
            .Select(p => p.Name)
            .Should()
            .Equal(nameof(OrderItem.Region), nameof(OrderItem.CreatedAt));
    }

    [Fact]
    public void LsiByCreatedAt_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByCreatedAt");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index.Properties.Select(p => p.Name).Should().Equal(nameof(OrderItem.CreatedAt));
    }

    [Fact]
    public void LsiByPriority_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByPriority");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index.Properties.Select(p => p.Name).Should().Equal(nameof(OrderItem.Priority));
    }
}
