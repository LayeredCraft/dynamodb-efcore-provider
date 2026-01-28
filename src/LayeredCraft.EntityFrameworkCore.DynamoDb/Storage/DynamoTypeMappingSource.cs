using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoTypeMappingSource : TypeMappingSource
{
    public DynamoTypeMappingSource(TypeMappingSourceDependencies dependencies) :
        base(dependencies) { }

    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        if (clrType == null)
            return null;

        // Handle nullable types by unwrapping
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        // Create converter for supported primitive types
        var converter = CreateConverter(nonNullableType);
        if (converter != null)
            return new DynamoTypeMapping(clrType).WithComposedConverter(converter);

        return null;
    }

    private static ValueConverter? CreateConverter(Type type)
        =>
            // Map each CLR type to its corresponding AttributeValue converter
            type switch
            {
                not null when type == typeof(string) => new AttributeValueToStringConverter(),
                not null when type == typeof(bool) => new AttributeValueToBoolConverter(),
                not null when type == typeof(int) => new AttributeValueToIntConverter(),
                not null when type == typeof(long) => new AttributeValueToLongConverter(),
                not null when type == typeof(short) => new AttributeValueToShortConverter(),
                not null when type == typeof(byte) => new AttributeValueToByteConverter(),
                not null when type == typeof(double) => new AttributeValueToDoubleConverter(),
                not null when type == typeof(float) => new AttributeValueToFloatConverter(),
                not null when type == typeof(decimal) => new AttributeValueToDecimalConverter(),
                not null when type == typeof(Guid) => new AttributeValueToGuidConverter(),
                not null when type == typeof(DateTime) => new AttributeValueToDateTimeConverter(),
                not null when type == typeof(DateTimeOffset) =>
                    new AttributeValueToDateTimeOffsetConverter(),
                _ => null,
            };
}
