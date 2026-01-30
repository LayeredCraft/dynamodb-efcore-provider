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
        ValueComparer? keyComparer = null) : base(
        new CoreTypeMappingParameters(clrType, null, comparer, keyComparer)) { }

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

    /// <summary>
    /// Generates a SQL literal for a constant value in PartiQL.
    /// </summary>
    public virtual string GenerateConstant(object? value)
    {
        // Apply value converter if present
        if (Converter != null && value != null)
            value = Converter.ConvertToProvider(value);

        if (value == null)
            return "NULL";

        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'", // Escape single quotes
            bool b => b ? "TRUE" : "FALSE",
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            short sh => sh.ToString(System.Globalization.CultureInfo.InvariantCulture),
            byte by => by.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Guid g => $"'{g}'",
            DateTime dt => $"'{dt:O}'",
            DateTimeOffset dto => $"'{dto:O}'",
            _ => throw new NotSupportedException(
                $"Type {value.GetType()} is not supported for SQL constant generation"),
        };
    }
}
