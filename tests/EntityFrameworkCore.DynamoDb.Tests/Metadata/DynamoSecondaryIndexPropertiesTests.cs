#if NET11_0
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>
///     Tests <see cref="DynamoSecondaryIndexProperties.AsScalar" />, the EF Core 11 guard against
///     complex-type members reaching secondary-index key handling. EF Core 11 widened
///     <c>IReadOnlyIndex.Properties</c>' element type from <see cref="IReadOnlyProperty" /> to
///     <see cref="IReadOnlyPropertyBase" /> so indexes can traverse complex-type properties;
///     DynamoDB secondary-index keys must remain scalar. This behavior is only reachable on EF
///     Core 11 — on EF10, <c>IReadOnlyIndex.Properties</c> is still
///     <c>IReadOnlyList&lt;IReadOnlyProperty&gt;</c>, so a non-scalar member can never appear.
/// </summary>
public class DynamoSecondaryIndexPropertiesTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void AsScalar_ScalarProperty_ReturnsSameInstance()
    {
        var property = Substitute.For<IReadOnlyProperty>();
        property.Name.Returns("Priority");

        var result = DynamoSecondaryIndexProperties.AsScalar(
            "Order",
            "ByPriority",
            property,
            "global secondary index partition key");

        result.Should().BeSameAs(property);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void AsScalar_ComplexTypeMember_ThrowsWithProviderLevelMessage()
    {
        var complexMember = Substitute.For<IReadOnlyPropertyBase>();
        complexMember.Name.Returns("ShippingAddress");

        var act = () => DynamoSecondaryIndexProperties.AsScalar(
            "Order",
            "ByPriority",
            complexMember,
            "global secondary index partition key");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Order*ByPriority*ShippingAddress*global secondary index partition key*scalar*");
    }
}
#endif
