using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Extensions;

public class DynamoEntityTypeExtensionsTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DynamoTableName_metadata_apis_should_round_trip_configuration()
    {
        var entityType = new ModelBuilder().Entity<TestEntity>().Metadata;

        entityType.SetDynamoTableName("TestTable");

        entityType.FindAnnotation(DynamoAnnotationNames.TableName)!.Value.Should().Be("TestTable");
        ((IConventionEntityType)entityType)
            .GetDynamoTableNameConfigurationSource()
            .Should()
            .Be(ConfigurationSource.Explicit);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CanSetDynamoTable_should_respect_configuration_source()
    {
        var entityType = (IConventionEntityType)new ModelBuilder().Entity<TestEntity>().Metadata;
        var builder = entityType.Builder;

        builder.CanSetDynamoTable("TestTable").Should().BeTrue();

        builder.ToDynamoTable("TestTable").Should().NotBeNull();
        builder.CanSetDynamoTable("OtherTable").Should().BeTrue();

        // Explicit configuration cannot be overridden by a data annotation.
        ((IMutableEntityType)entityType).SetDynamoTableName("TestTable");
        builder.CanSetDynamoTable("OtherTable", fromDataAnnotation: true).Should().BeFalse();
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
    }
}
