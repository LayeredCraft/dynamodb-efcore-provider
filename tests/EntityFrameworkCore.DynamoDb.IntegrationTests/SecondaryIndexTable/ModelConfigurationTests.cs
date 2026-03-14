using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Verifies that the EF Core model metadata is built correctly for <c>OrderItem</c>.
///     These tests exercise the public model-building API only — runtime descriptor internals are
///     covered by the unit test suite.
/// </summary>
public class ModelConfigurationTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void FourSecondaryIndexes_AreRegisteredOnEntityType()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;

        entityType
            .GetIndexes()
            .Where(i => i.GetSecondaryIndexName() is not null)
            .Should()
            .HaveCount(4);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void LsiByCreatedAt_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByCreatedAt");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index.Properties.Select(p => p.Name).Should().Equal(nameof(OrderItem.CreatedAt));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void LsiByPriority_HasCorrectMetadata()
    {
        var entityType = Db.Model.FindEntityType(typeof(OrderItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetSecondaryIndexName() == "ByPriority");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
        index.Properties.Select(p => p.Name).Should().Equal(nameof(OrderItem.Priority));
    }
}
