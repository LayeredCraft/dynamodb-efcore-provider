using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoTypeMapping : CoreTypeMapping
{
    public DynamoTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null,
        CoreTypeMapping? elementMapping = null,
        JsonValueReaderWriter? jsonValueReaderWriter = null) : base(
        new CoreTypeMappingParameters(
            clrType,
            null,
            comparer,
            keyComparer,
            elementMapping: elementMapping,
            jsonValueReaderWriter: jsonValueReaderWriter)) { }

    protected DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters) { }

    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new DynamoTypeMapping(parameters);

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
}
