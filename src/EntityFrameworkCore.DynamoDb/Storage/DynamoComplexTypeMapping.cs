using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
///     DynamoDB type mapping for whole complex type values used in query parameters and constants.
/// </summary>
internal sealed class DynamoComplexTypeMapping(Type clrType, IComplexType complexType)
    : DynamoTypeMapping(clrType)
{
    internal override bool CanWriteToAttributeValue => true;

    internal override bool RequiresParameterForPartiQlLiteral => true;

    internal override AttributeValue CreateAttributeValue(object? value)
        => EntityWritePlan.SerializeComplexTypeValue(value, complexType);

    internal override AttributeValue CreateAttributeValue(object? value, Type sourceType)
        => EntityWritePlan.SerializeComplexTypeValue(value, complexType);

    public override string GenerateConstant(object? value)
        => throw new NotSupportedException(
            "Complex type constants must be sent as DynamoDB query parameters.");

    internal override string GenerateConstant(object? value, Type sourceType)
        => throw new NotSupportedException(
            "Complex type constants must be sent as DynamoDB query parameters.");

    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new DynamoComplexTypeMapping(parameters.ClrType, complexType);
}
