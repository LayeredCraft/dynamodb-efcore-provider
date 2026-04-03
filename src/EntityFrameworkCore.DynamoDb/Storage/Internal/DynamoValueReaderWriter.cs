using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>Converts a single mapped CLR value to and from DynamoDB wire representations.</summary>
/// <remarks>
///     Mappings own these instances so query materialization, query parameter generation, and
///     SaveChanges writes all share the same conversion rules.
/// </remarks>
internal abstract class DynamoValueReaderWriter
{
    private static readonly MethodInfo ReadAsObjectMethod =
        typeof(DynamoValueReaderWriter).GetMethod(
            nameof(ReadAsObject),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    public abstract Type ValueType { get; }

    internal abstract string WireMemberName { get; }

    internal Expression CreateReadExpression(
            Expression attributeValueExpression,
            string propertyPath,
            bool required,
            IProperty? property)
        // Keep the read path expression-based: the compiled query closes over this mapping-owned
        // reader/writer instance instead of re-deriving wire-shape rules elsewhere.
        => Expression.Convert(
            Expression.Call(
                Expression.Constant(this),
                ReadAsObjectMethod,
                attributeValueExpression,
                Expression.Constant(propertyPath),
                Expression.Constant(required),
                Expression.Constant(property, typeof(IProperty))),
            ValueType);

    protected abstract object? ReadAsObject(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property);

    public abstract AttributeValue WriteAsObject(object value);

    public abstract string ToPartiQlLiteralAsObject(object value);

    protected string CreateMissingValueMessage(string propertyPath)
        => $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{WireMemberName}'.";
}

/// <summary>Strongly-typed base implementation for a DynamoDB value reader/writer.</summary>
internal abstract class DynamoValueReaderWriter<TValue> : DynamoValueReaderWriter
{
    public sealed override Type ValueType => typeof(TValue);

    protected sealed override object? ReadAsObject(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property)
        => Read(attributeValue, propertyPath, required, property);

    public sealed override AttributeValue WriteAsObject(object value) => Write((TValue)value);

    public sealed override string ToPartiQlLiteralAsObject(object value)
        => ToPartiQlLiteral((TValue)value);

    public TValue Read(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        if (!HasValue(attributeValue))
        {
            if (required)
                throw new InvalidOperationException(CreateMissingValueMessage(propertyPath));

            return default!;
        }

        return ReadValue(attributeValue, propertyPath, property);
    }

    internal abstract bool HasValue(AttributeValue attributeValue);

    protected abstract TValue ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property);

    public abstract AttributeValue Write(TValue value);

    public abstract string ToPartiQlLiteral(TValue value);
}

/// <summary>Exposes the provider-level reader/writer under a composed wrapper.</summary>
/// <remarks>
///     This allows mapping construction to peel back previously composed wrappers and apply the
///     current converter exactly once, mirroring EF Core's converted reader/writer patterns.
/// </remarks>
internal interface IDynamoConvertedValueReaderWriter
{
    DynamoValueReaderWriter InnerReaderWriter { get; }
}

/// <summary>Wraps a provider-level reader/writer with an EF Core <see cref="ValueConverter" />.</summary>
internal sealed class DynamoConvertedValueReaderWriter<TModel, TProvider>(
    DynamoValueReaderWriter<TProvider> innerReaderWriter,
    ValueConverter converter) : DynamoValueReaderWriter<TModel>, IDynamoConvertedValueReaderWriter
{
    internal override string WireMemberName => innerReaderWriter.WireMemberName;

    DynamoValueReaderWriter IDynamoConvertedValueReaderWriter.InnerReaderWriter
        => innerReaderWriter;

    internal override bool HasValue(AttributeValue attributeValue)
        => innerReaderWriter.HasValue(attributeValue);

    protected override TModel ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => (TModel)converter.ConvertFromProvider(
            innerReaderWriter.Read(attributeValue, propertyPath, true, property))!;

    public override AttributeValue Write(TModel value)
        => innerReaderWriter.Write((TProvider)converter.ConvertToProvider(value)!);

    public override string ToPartiQlLiteral(TModel value)
        => innerReaderWriter.ToPartiQlLiteral((TProvider)converter.ConvertToProvider(value)!);
}

/// <summary>Adapts a non-nullable provider reader/writer for nullable value-type mappings.</summary>
/// <remarks>
///     This is used both for scalar properties and for primitive collection element mappings such
///     as <c>List&lt;int?&gt;</c>, where DynamoDB NULL must round-trip as a nullable CLR value.
/// </remarks>
internal sealed class NullableDynamoValueReaderWriter<TValue>(
    DynamoValueReaderWriter<TValue> innerReaderWriter) : DynamoValueReaderWriter<TValue?>
    where TValue : struct
{
    internal override string WireMemberName => innerReaderWriter.WireMemberName;

    internal override bool HasValue(AttributeValue attributeValue)
        => attributeValue.NULL == true || innerReaderWriter.HasValue(attributeValue);

    protected override TValue? ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.NULL == true
            ? null
            : innerReaderWriter.Read(attributeValue, propertyPath, true, property);

    public override AttributeValue Write(TValue? value)
        => value.HasValue
            ? innerReaderWriter.Write(value.Value)
            : new AttributeValue { NULL = true };

    public override string ToPartiQlLiteral(TValue? value)
        => value.HasValue ? innerReaderWriter.ToPartiQlLiteral(value.Value) : "NULL";
}

internal sealed class StringDynamoValueReaderWriter : DynamoValueReaderWriter<string>
{
    internal override string WireMemberName => nameof(AttributeValue.S);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.S != null;

    protected override string ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.S;

    public override AttributeValue Write(string value) => new() { S = value };

    public override string ToPartiQlLiteral(string value)
        => DynamoValueReaderWriterHelpers.FormatStringLiteral(value);
}

internal sealed class BoolDynamoValueReaderWriter : DynamoValueReaderWriter<bool>
{
    internal override string WireMemberName => nameof(AttributeValue.BOOL);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.BOOL != null;

    protected override bool ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.BOOL!.Value;

    public override AttributeValue Write(bool value) => new() { BOOL = value };

    public override string ToPartiQlLiteral(bool value) => value ? "TRUE" : "FALSE";
}

internal sealed class BinaryDynamoValueReaderWriter : DynamoValueReaderWriter<byte[]>
{
    internal override string WireMemberName => nameof(AttributeValue.B);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.B != null;

    protected override byte[] ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.B!.ToArray();

    public override AttributeValue Write(byte[] value)
        => new() { B = new MemoryStream(value, false) };

    public override string ToPartiQlLiteral(byte[] value)
        => throw new NotSupportedException(
            "Binary values are not supported for inline PartiQL constant generation.");
}

internal sealed class NumericDynamoValueReaderWriter<TValue>(
    Func<string, TValue> parse,
    Func<TValue, string> format) : DynamoValueReaderWriter<TValue>
{
    internal override string WireMemberName => nameof(AttributeValue.N);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.N != null;

    protected override TValue ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => parse(attributeValue.N);

    public override AttributeValue Write(TValue value) => new() { N = format(value) };

    public override string ToPartiQlLiteral(TValue value) => format(value);
}

internal sealed class ListDynamoValueReaderWriter<TCollection, TElement>(
    DynamoValueReaderWriter<TElement> elementReaderWriter) : DynamoValueReaderWriter<TCollection>
{
    internal override string WireMemberName => nameof(AttributeValue.L);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.L != null;

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadList<TCollection, TElement>(
            attributeValue,
            propertyPath,
            property,
            elementReaderWriter);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteList(
            (IEnumerable<TElement>)value!,
            elementReaderWriter);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatListLiteral(
            (IEnumerable<TElement>)value!,
            elementReaderWriter);
}

internal sealed class DictionaryDynamoValueReaderWriter<TCollection, TValue>(
    DynamoValueReaderWriter<TValue> valueReaderWriter,
    bool readOnly) : DynamoValueReaderWriter<TCollection>
{
    internal override string WireMemberName => nameof(AttributeValue.M);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.M != null;

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadDictionary<TCollection, TValue>(
            attributeValue,
            propertyPath,
            property,
            valueReaderWriter,
            readOnly);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteDictionary(
            (IEnumerable<KeyValuePair<string, TValue>>)value!,
            valueReaderWriter);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatDictionaryLiteral(
            (IEnumerable<KeyValuePair<string, TValue>>)value!,
            valueReaderWriter);
}

internal sealed class SetDynamoValueReaderWriter<TCollection, TElement>(
    DynamoValueReaderWriter<TElement> elementReaderWriter) : DynamoValueReaderWriter<TCollection>
{
    internal override string WireMemberName { get; } =
        DynamoValueReaderWriterHelpers.GetSetWireMemberName(elementReaderWriter.WireMemberName);

    internal override bool HasValue(AttributeValue attributeValue)
        => DynamoValueReaderWriterHelpers.HasSetValue(attributeValue, WireMemberName);

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadSet<TCollection, TElement>(
            attributeValue,
            propertyPath,
            property,
            elementReaderWriter,
            WireMemberName);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteSet(
            (IEnumerable<TElement>)value!,
            elementReaderWriter,
            WireMemberName);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatSetLiteral(
            (IEnumerable<TElement>)value!,
            elementReaderWriter);
}

/// <summary>Builds reader/writers for mapping CLR types and composes them with EF Core converters.</summary>
internal static class DynamoValueReaderWriterFactory
{
    private static readonly DynamoValueReaderWriter StringReaderWriter =
        new StringDynamoValueReaderWriter();

    private static readonly DynamoValueReaderWriter BoolReaderWriter =
        new BoolDynamoValueReaderWriter();

    private static readonly DynamoValueReaderWriter BinaryReaderWriter =
        new BinaryDynamoValueReaderWriter();

    private static readonly MethodInfo CreateListReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateListReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateDictionaryReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateDictionaryReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateSetReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateSetReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateNullableReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateNullableReaderWriterGeneric),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    public static DynamoValueReaderWriter? Create(
        Type clrType,
        DynamoValueReaderWriter? elementReaderWriter = null,
        bool readOnlyDictionary = false)
    {
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        var isNullableValueType = clrType != nonNullableType && nonNullableType.IsValueType;

        if (nonNullableType == typeof(string))
            return StringReaderWriter;

        if (nonNullableType == typeof(bool))
            return isNullableValueType
                ? CreateNullableReaderWriter(nonNullableType, BoolReaderWriter)
                : BoolReaderWriter;

        if (nonNullableType == typeof(byte[]))
            return BinaryReaderWriter;

        if (TryCreateNumeric(nonNullableType, out var numericReaderWriter))
            return isNullableValueType
                ? CreateNullableReaderWriter(nonNullableType, numericReaderWriter)
                : numericReaderWriter;

        // Collection mappings are built from the element/value mapping that EF resolved earlier,
        // so rich shapes inherit the same converter and wire-format behavior as their elements.
        if (elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
            return (DynamoValueReaderWriter)CreateListReaderWriterMethod
                .MakeGenericMethod(clrType, listElementType)
                .Invoke(null, [elementReaderWriter])!;

        if (elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetDictionaryValueType(
                clrType,
                out var dictionaryValueType,
                out _))
            return (DynamoValueReaderWriter)CreateDictionaryReaderWriterMethod
                .MakeGenericMethod(clrType, dictionaryValueType)
                .Invoke(null, [elementReaderWriter, readOnlyDictionary])!;

        if (elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
            return (DynamoValueReaderWriter)CreateSetReaderWriterMethod
                .MakeGenericMethod(clrType, setElementType)
                .Invoke(null, [elementReaderWriter])!;

        return null;
    }

    public static DynamoValueReaderWriter? Compose(
        ValueConverter? converter,
        DynamoValueReaderWriter? readerWriter)
    {
        if (readerWriter is IDynamoConvertedValueReaderWriter converted)
            readerWriter = converted.InnerReaderWriter;

        if (converter == null || readerWriter == null)
            return readerWriter;

        // Compose the converter once at mapping-construction time so callers can keep working with
        // model CLR values while the inner reader/writer stays focused on the provider CLR type.
        return (DynamoValueReaderWriter)Activator.CreateInstance(
            typeof(DynamoConvertedValueReaderWriter<,>).MakeGenericType(
                converter.ModelClrType,
                readerWriter.ValueType),
            readerWriter,
            converter)!;
    }

    private static ListDynamoValueReaderWriter<TCollection, TElement>
        CreateListReaderWriter<TCollection, TElement>(DynamoValueReaderWriter elementReaderWriter)
        => new(CoerceReaderWriter<TElement>(elementReaderWriter));

    private static DictionaryDynamoValueReaderWriter<TCollection, TValue>
        CreateDictionaryReaderWriter<TCollection, TValue>(
            DynamoValueReaderWriter valueReaderWriter,
            bool readOnly)
        => new(CoerceReaderWriter<TValue>(valueReaderWriter), readOnly);

    private static SetDynamoValueReaderWriter<TCollection, TElement>
        CreateSetReaderWriter<TCollection, TElement>(DynamoValueReaderWriter elementReaderWriter)
        => new(CoerceReaderWriter<TElement>(elementReaderWriter));

    private static DynamoValueReaderWriter CreateNullableReaderWriter(
        Type valueType,
        DynamoValueReaderWriter readerWriter)
        => (DynamoValueReaderWriter)CreateNullableReaderWriterMethod
            .MakeGenericMethod(valueType)
            .Invoke(null, [readerWriter])!;

    private static NullableDynamoValueReaderWriter<TValue>
        CreateNullableReaderWriterGeneric<TValue>(DynamoValueReaderWriter readerWriter)
        where TValue : struct
        => new((DynamoValueReaderWriter<TValue>)readerWriter);

    private static DynamoValueReaderWriter<TValue> CoerceReaderWriter<TValue>(
        DynamoValueReaderWriter readerWriter)
    {
        if (readerWriter is DynamoValueReaderWriter<TValue> typedReaderWriter)
            return typedReaderWriter;

        var underlyingType = Nullable.GetUnderlyingType(typeof(TValue));
        if (underlyingType != null && readerWriter.ValueType == underlyingType)
            // Primitive collection metadata often flows the non-nullable element mapping even when
            // the collection CLR element is nullable. Wrap it here so list/set/dictionary readers
            // can still materialize and write NULL collection elements consistently.
            return (DynamoValueReaderWriter<TValue>)CreateNullableReaderWriter(
                underlyingType,
                readerWriter);

        throw new InvalidCastException(
            $"Unable to use DynamoDB reader/writer for '{readerWriter.ValueType.Name}' as '{typeof(TValue).Name}'.");
    }

    private static bool TryCreateNumeric(
        Type clrType,
        [NotNullWhen(true)] out DynamoValueReaderWriter? readerWriter)
    {
        readerWriter = clrType switch
        {
            _ when clrType == typeof(byte) => new NumericDynamoValueReaderWriter<byte>(
                static value
                    => byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(sbyte) => new NumericDynamoValueReaderWriter<sbyte>(
                static value
                    => sbyte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(short) => new NumericDynamoValueReaderWriter<short>(
                static value
                    => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(ushort) => new NumericDynamoValueReaderWriter<ushort>(
                static value
                    => ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(int) => new NumericDynamoValueReaderWriter<int>(
                static value
                    => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(uint) => new NumericDynamoValueReaderWriter<uint>(
                static value
                    => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(long) => new NumericDynamoValueReaderWriter<long>(
                static value
                    => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(ulong) => new NumericDynamoValueReaderWriter<ulong>(
                static value
                    => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(float) => new NumericDynamoValueReaderWriter<float>(
                static value
                    => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString("R", CultureInfo.InvariantCulture)),

            _ when clrType == typeof(double) => new NumericDynamoValueReaderWriter<double>(
                static value
                    => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString("R", CultureInfo.InvariantCulture)),

            _ when clrType == typeof(decimal) => new NumericDynamoValueReaderWriter<decimal>(
                static value
                    => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ => null,
        };

        return readerWriter != null;
    }
}

/// <summary>Shared helpers for collection materialization and PartiQL literal formatting.</summary>
internal static class DynamoValueReaderWriterHelpers
{
    public static string FormatStringLiteral(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    public static TCollection ReadList<TCollection, TElement>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
    {
        // Collection element nullability comes from element metadata, not the collection property
        // itself. Required value-type elements should fail when a wire value is missing.
        var elementRequired = IsRequiredCollectionElement(property, typeof(TElement));
        var result = new List<TElement>(attributeValue.L.Count);

        foreach (var element in attributeValue.L)
            result.Add(elementReaderWriter.Read(element, propertyPath, elementRequired, null));

        return ConvertListResult<TCollection, TElement>(result);
    }

    public static AttributeValue WriteList<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
        => new() { L = value.Select(elementReaderWriter.Write).ToList() };

    public static string FormatListLiteral<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
        => $"[{string.Join(", ", value.Select(elementReaderWriter.ToPartiQlLiteral))}]";

    public static TCollection ReadDictionary<TCollection, TValue>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TValue> valueReaderWriter,
        bool readOnly)
    {
        var valueRequired = IsRequiredCollectionElement(property, typeof(TValue));
        var result = new Dictionary<string, TValue>(attributeValue.M.Count, StringComparer.Ordinal);

        foreach (var pair in attributeValue.M)
            result.Add(
                pair.Key,
                valueReaderWriter.Read(pair.Value, propertyPath, valueRequired, null));

        return ConvertDictionaryResult<TCollection, TValue>(result, readOnly);
    }

    public static AttributeValue WriteDictionary<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        DynamoValueReaderWriter<TValue> valueReaderWriter)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

        foreach (var pair in value)
            map[pair.Key] = valueReaderWriter.Write(pair.Value);

        return new AttributeValue { M = map };
    }

    public static string FormatDictionaryLiteral<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        DynamoValueReaderWriter<TValue> valueReaderWriter)
        => $"{{{string.Join(", ", value.Select(
            pair => $"{FormatStringLiteral(pair.Key)}: {valueReaderWriter.ToPartiQlLiteral(pair.Value)}"))}}}";

    public static bool HasSetValue(AttributeValue attributeValue, string wireMemberName)
        => wireMemberName == nameof(AttributeValue.SS) ? attributeValue.SS != null :
            wireMemberName == nameof(AttributeValue.NS) ? attributeValue.NS != null :
            attributeValue.BS != null;

    public static string GetSetWireMemberName(string elementWireMemberName)
        => elementWireMemberName == nameof(AttributeValue.S) ? nameof(AttributeValue.SS) :
            elementWireMemberName == nameof(AttributeValue.B) ? nameof(AttributeValue.BS) :
            nameof(AttributeValue.NS);

    public static TCollection ReadSet<TCollection, TElement>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TElement> elementReaderWriter,
        string setWireMemberName)
    {
        var elementRequired = IsRequiredCollectionElement(property, typeof(TElement));
        var result = new HashSet<TElement>();

        if (setWireMemberName == nameof(AttributeValue.SS))
            foreach (var value in attributeValue.SS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { S = value },
                        propertyPath,
                        elementRequired,
                        null));
        else if (setWireMemberName == nameof(AttributeValue.NS))
            foreach (var value in attributeValue.NS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { N = value },
                        propertyPath,
                        elementRequired,
                        null));
        else
            foreach (var value in attributeValue.BS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { B = value },
                        propertyPath,
                        elementRequired,
                        null));

        return ConvertSetResult<TCollection, TElement>(result);
    }

    public static AttributeValue WriteSet<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter,
        string setWireMemberName)
    {
        var writtenValues = value.Select(elementReaderWriter.Write).ToList();
        if (writtenValues.Count == 0)
            // DynamoDB does not allow empty SS/NS/BS attributes.
            return new AttributeValue { NULL = true };

        if (setWireMemberName == nameof(AttributeValue.SS))
            return new AttributeValue
            {
                SS = writtenValues.Select(attributeValue => attributeValue.S).ToList(),
            };

        if (setWireMemberName == nameof(AttributeValue.NS))
            return new AttributeValue
            {
                NS = writtenValues.Select(attributeValue => attributeValue.N).ToList(),
            };

        return new AttributeValue
        {
            BS = writtenValues.Select(attributeValue => attributeValue.B).ToList(),
        };
    }

    public static string FormatSetLiteral<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
    {
        var literals = value.Select(elementReaderWriter.ToPartiQlLiteral).ToList();
        // DynamoDB cannot represent an empty set literal, so keep the SQL/literal path aligned
        // with parameter serialization by treating empty sets as NULL.
        return literals.Count == 0 ? "NULL" : $"<<{string.Join(", ", literals)}>>";
    }

    private static TCollection ConvertListResult<TCollection, TElement>(List<TElement> values)
        => typeof(TCollection).IsArray
            ? (TCollection)(object)values.ToArray()
            : (TCollection)(object)values;

    private static TCollection ConvertDictionaryResult<TCollection, TValue>(
        Dictionary<string, TValue> values,
        bool readOnly)
    {
        if (readOnly)
            return (TCollection)(object)new ReadOnlyDictionary<string, TValue>(values);

        return (TCollection)(object)values;
    }

    private static TCollection ConvertSetResult<TCollection, TElement>(HashSet<TElement> values)
        => (TCollection)(object)values;

    private static bool IsRequiredCollectionElement(IProperty? property, Type elementType)
        => property?.GetElementType()?.IsNullable == false
            || (elementType.IsValueType && Nullable.GetUnderlyingType(elementType) == null);
}
