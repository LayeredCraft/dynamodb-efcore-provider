using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Represents the DynamoTypeMapping type.</summary>
public class DynamoTypeMapping : CoreTypeMapping
{
    /// <summary>Provides functionality for this member.</summary>
    public DynamoTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null) : base(
        new CoreTypeMappingParameters(clrType, null, comparer, keyComparer)) { }

    /// <summary>Provides functionality for this member.</summary>
    protected DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters) { }

    /// <summary>Provides functionality for this member.</summary>
    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new DynamoTypeMapping(parameters);

    /// <summary>Provides functionality for this member.</summary>
    public override CoreTypeMapping WithComposedConverter(
        ValueConverter? converter,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null,
        CoreTypeMapping? elementMapping = null,
        JsonValueReaderWriter? jsonValueReaderWriter = null)
        => new DynamoTypeMapping(
            Parameters.WithComposedConverter(
                converter,
                comparer,
                keyComparer,
                elementMapping,
                jsonValueReaderWriter));

    /// <summary>
    /// Generates a SQL literal for a constant value in PartiQL.
    /// </summary>
    public virtual string GenerateConstant(object? value)
    {
        if (Converter != null && value != null)
            value = Converter.ConvertToProvider(value);

        return DynamoWireValueConversion.GenerateBoxedConstant(value);
    }
}
