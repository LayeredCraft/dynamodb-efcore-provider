using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Type mapping source for DynamoDB that maps only CLR wire primitives.
/// Returns null for non-primitive types, allowing EF Core to compose converters automatically.
/// </summary>
public class DynamoTypeMappingSource : TypeMappingSource
{
    public DynamoTypeMappingSource(TypeMappingSourceDependencies dependencies) :
        base(dependencies) { }

    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        if (clrType == null)
            return null;

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        // Map ONLY wire primitives that AttributeValue directly supports
        // EF Core will automatically compose converters for non-primitive types:
        // - int, short, byte → long (via EF Core converter)
        // - float → double (via EF Core converter)
        // - DateTime, DateTimeOffset → string (via EF Core converter)
        // - Guid → string (via EF Core converter)
        // - Enums → int/long/string (via EF Core converter)
        if (IsPrimitiveType(nonNullableType))
        {
            var jsonReaderWriter =
                Dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType);
            return new DynamoTypeMapping(clrType, jsonValueReaderWriter: jsonReaderWriter);
        }

        // Return null for all other types - EF Core will compose converters
        return null;
    }

    /// <summary>
    /// Determines if a type is a wire primitive that DynamoDB's AttributeValue natively supports.
    /// </summary>
    private static bool IsPrimitiveType(Type type)
        => type == typeof(string) // AttributeValue.S
           || type == typeof(bool) // AttributeValue.BOOL
           || type == typeof(long) // AttributeValue.N (primary integer wire type)
           || type == typeof(double) // AttributeValue.N (floating point wire type)
           || type == typeof(decimal) // AttributeValue.N (high precision wire type)
           || type == typeof(byte[]); // AttributeValue.B (binary wire type)
}
