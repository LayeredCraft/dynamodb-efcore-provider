using System.Collections;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Shared AOT-safe dispatch helpers for DynamoDB write serializers.</summary>
internal static class DynamoWriteValueSerializer
{
    /// <summary>Dispatches a DynamoDB wire CLR type to a closed generic serializer factory.</summary>
    internal static Func<IUpdateEntry, AttributeValue>
        DispatchWireType<TFactory>(IProperty property, Type type, TFactory factory)
        where TFactory : struct, IDynamoWriteValueSerializerFactory
        => TryDispatchWireType(property, type, factory)
            ?? throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}' has unsupported "
                + $"wire type '{type.ShortDisplayName()}' on the write path. This is a provider bug — "
                + "the type mapping layer should have ensured a DynamoDB-native provider type.");

    /// <summary>Attempts to dispatch a DynamoDB wire CLR type to a closed generic serializer factory.</summary>
    internal static Func<IUpdateEntry, AttributeValue>? TryDispatchWireType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, IDynamoWriteValueSerializerFactory
    {
        if (type == typeof(string))
            return factory.Create<string>(property);
        if (type == typeof(bool))
            return factory.Create<bool>(property);
        if (type == typeof(bool?))
            return factory.Create<bool?>(property);
        if (type == typeof(byte))
            return factory.Create<byte>(property);
        if (type == typeof(byte?))
            return factory.Create<byte?>(property);
        if (type == typeof(sbyte))
            return factory.Create<sbyte>(property);
        if (type == typeof(sbyte?))
            return factory.Create<sbyte?>(property);
        if (type == typeof(short))
            return factory.Create<short>(property);
        if (type == typeof(short?))
            return factory.Create<short?>(property);
        if (type == typeof(ushort))
            return factory.Create<ushort>(property);
        if (type == typeof(ushort?))
            return factory.Create<ushort?>(property);
        if (type == typeof(int))
            return factory.Create<int>(property);
        if (type == typeof(int?))
            return factory.Create<int?>(property);
        if (type == typeof(uint))
            return factory.Create<uint>(property);
        if (type == typeof(uint?))
            return factory.Create<uint?>(property);
        if (type == typeof(long))
            return factory.Create<long>(property);
        if (type == typeof(long?))
            return factory.Create<long?>(property);
        if (type == typeof(ulong))
            return factory.Create<ulong>(property);
        if (type == typeof(ulong?))
            return factory.Create<ulong?>(property);
        if (type == typeof(float))
            return factory.Create<float>(property);
        if (type == typeof(float?))
            return factory.Create<float?>(property);
        if (type == typeof(double))
            return factory.Create<double>(property);
        if (type == typeof(double?))
            return factory.Create<double?>(property);
        if (type == typeof(decimal))
            return factory.Create<decimal>(property);
        if (type == typeof(decimal?))
            return factory.Create<decimal?>(property);
        if (type == typeof(byte[]))
            return factory.Create<byte[]>(property);

        return null;
    }

    /// <summary>Attempts to dispatch a converter model CLR type to a closed generic serializer factory.</summary>
    internal static Func<IUpdateEntry, AttributeValue>? TryDispatchModelType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, IDynamoWriteValueSerializerFactory
    {
        var wireResult = TryDispatchWireType(property, type, factory);
        if (wireResult is not null)
            return wireResult;

        if (type == typeof(Guid))
            return factory.Create<Guid>(property);
        if (type == typeof(Guid?))
            return factory.Create<Guid?>(property);
        if (type == typeof(DateTime))
            return factory.Create<DateTime>(property);
        if (type == typeof(DateTime?))
            return factory.Create<DateTime?>(property);
        if (type == typeof(DateTimeOffset))
            return factory.Create<DateTimeOffset>(property);
        if (type == typeof(DateTimeOffset?))
            return factory.Create<DateTimeOffset?>(property);
        if (type == typeof(TimeSpan))
            return factory.Create<TimeSpan>(property);
        if (type == typeof(TimeSpan?))
            return factory.Create<TimeSpan?>(property);
        if (type == typeof(DateOnly))
            return factory.Create<DateOnly>(property);
        if (type == typeof(DateOnly?))
            return factory.Create<DateOnly?>(property);
        if (type == typeof(TimeOnly))
            return factory.Create<TimeOnly>(property);
        if (type == typeof(TimeOnly?))
            return factory.Create<TimeOnly?>(property);

        return null;
    }

    /// <summary>Attempts to dispatch a non-nullable converter model struct type.</summary>
    internal static Func<IUpdateEntry, AttributeValue>? TryDispatchStructModelType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, IDynamoWriteStructValueSerializerFactory
    {
        if (type == typeof(bool))
            return factory.Create<bool>(property);
        if (type == typeof(byte))
            return factory.Create<byte>(property);
        if (type == typeof(sbyte))
            return factory.Create<sbyte>(property);
        if (type == typeof(short))
            return factory.Create<short>(property);
        if (type == typeof(ushort))
            return factory.Create<ushort>(property);
        if (type == typeof(int))
            return factory.Create<int>(property);
        if (type == typeof(uint))
            return factory.Create<uint>(property);
        if (type == typeof(long))
            return factory.Create<long>(property);
        if (type == typeof(ulong))
            return factory.Create<ulong>(property);
        if (type == typeof(float))
            return factory.Create<float>(property);
        if (type == typeof(double))
            return factory.Create<double>(property);
        if (type == typeof(decimal))
            return factory.Create<decimal>(property);
        if (type == typeof(Guid))
            return factory.Create<Guid>(property);
        if (type == typeof(DateTime))
            return factory.Create<DateTime>(property);
        if (type == typeof(DateTimeOffset))
            return factory.Create<DateTimeOffset>(property);
        if (type == typeof(TimeSpan))
            return factory.Create<TimeSpan>(property);
        if (type == typeof(DateOnly))
            return factory.Create<DateOnly>(property);
        if (type == typeof(TimeOnly))
            return factory.Create<TimeOnly>(property);

        return null;
    }

    /// <summary>Creates a DynamoDB null attribute value.</summary>
    internal static AttributeValue NullAttributeValue() => new() { NULL = true };
    /// <summary>Serializes a provider-shaped scalar or list value.</summary>
    internal static AttributeValue ConvertProviderShapeToAttributeValue(object? providerValue)
    {
        if (providerValue is null)
            return new AttributeValue { NULL = true };

        if (providerValue is string or byte[])
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(providerValue);

        if (providerValue is IEnumerable enumerable)
            return SerializeBoxedScalarList(enumerable);

        return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(providerValue);
    }

    /// <summary>Serializes a complex scalar dictionary with no element converter.</summary>
    internal static AttributeValue SerializeDirectScalarDictionary(object value, Type valueType)
    {
        if (valueType == typeof(string))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, string>>)value);
        if (valueType == typeof(bool))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, bool>>)value);
        if (valueType == typeof(bool?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, bool?>>)value);
        if (valueType == typeof(byte))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, byte>>)value);
        if (valueType == typeof(byte?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, byte?>>)value);
        if (valueType == typeof(sbyte))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, sbyte>>)value);
        if (valueType == typeof(sbyte?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, sbyte?>>)value);
        if (valueType == typeof(short))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, short>>)value);
        if (valueType == typeof(short?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, short?>>)value);
        if (valueType == typeof(ushort))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, ushort>>)value);
        if (valueType == typeof(ushort?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, ushort?>>)value);
        if (valueType == typeof(int))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, int>>)value);
        if (valueType == typeof(int?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, int?>>)value);
        if (valueType == typeof(uint))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, uint>>)value);
        if (valueType == typeof(uint?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, uint?>>)value);
        if (valueType == typeof(long))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, long>>)value);
        if (valueType == typeof(long?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, long?>>)value);
        if (valueType == typeof(ulong))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, ulong>>)value);
        if (valueType == typeof(ulong?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, ulong?>>)value);
        if (valueType == typeof(float))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, float>>)value);
        if (valueType == typeof(float?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, float?>>)value);
        if (valueType == typeof(double))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, double>>)value);
        if (valueType == typeof(double?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, double?>>)value);
        if (valueType == typeof(decimal))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, decimal>>)value);
        if (valueType == typeof(decimal?))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, decimal?>>)value);
        if (valueType == typeof(byte[]))
            return DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                (IEnumerable<KeyValuePair<string, byte[]>>)value);

        return SerializeBoxedScalarDictionary((IEnumerable)value);
    }

    /// <summary>Serializes a complex scalar dictionary using a value converter.</summary>
    internal static AttributeValue SerializeConvertedScalarDictionary(
        object value,
        ValueConverter valueConverter)
    {
        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in dictionary)
            {
                var providerValue = item.Value is null
                    ? null
                    : valueConverter.ConvertToProvider(item.Value);
                map[(string)item.Key] = ConvertProviderShapeToAttributeValue(providerValue);
            }

            return new AttributeValue { M = map };
        }

        return SerializeBoxedScalarDictionary(
            (IEnumerable)value,
            item =>
            {
                var providerValue = item is null ? null : valueConverter.ConvertToProvider(item);
                return ConvertProviderShapeToAttributeValue(providerValue);
            });
    }

    /// <summary>Serializes a complex scalar dictionary through boxed key-value pairs.</summary>
    private static AttributeValue SerializeBoxedScalarDictionary(IEnumerable value)
        => SerializeBoxedScalarDictionary(
            value,
            DynamoWireValueConversion.ConvertProviderValueToAttributeValue);

    /// <summary>Serializes a complex scalar dictionary through boxed key-value pairs.</summary>
    private static AttributeValue SerializeBoxedScalarDictionary(
        IEnumerable value,
        Func<object?, AttributeValue> serializeValue)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
                map[(string)item.Key] = serializeValue(item.Value);
            return new AttributeValue { M = map };
        }

        foreach (var item in value)
        {
            var (key, itemValue) = ReadKeyValuePair(item);
            map[key] = serializeValue(itemValue);
        }

        return new AttributeValue { M = map };
    }

    /// <summary>Serializes a complex scalar set with no element converter.</summary>
    internal static AttributeValue SerializeDirectScalarSet(object value, Type elementType)
    {
        if (elementType == typeof(string))
            return DynamoAttributeValueCollectionHelpers.SerializeSet((IEnumerable<string>)value);
        if (elementType == typeof(int))
            return DynamoAttributeValueCollectionHelpers.SerializeSet((IEnumerable<int>)value);
        if (elementType == typeof(byte[]))
            return DynamoAttributeValueCollectionHelpers.SerializeSet((IEnumerable<byte[]>)value);

        return SerializeBoxedScalarSet((IEnumerable)value);
    }

    /// <summary>Serializes a complex scalar set using an element converter.</summary>
    internal static AttributeValue SerializeConvertedScalarSet(
        object value,
        ValueConverter elementConverter)
    {
        var providerValues = new List<object>();
        foreach (var element in (IEnumerable)value)
        {
            if (element is null)
                throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");

            var providerValue = elementConverter.ConvertToProvider(element);
            if (providerValue is null)
                throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");
            providerValues.Add(providerValue);
        }

        return SerializeBoxedScalarSet(providerValues);
    }

    /// <summary>Serializes a complex scalar set through boxed scalar elements.</summary>
    private static AttributeValue SerializeBoxedScalarSet(IEnumerable value)
    {
        List<string>? ss = null;
        List<string>? ns = null;
        List<MemoryStream>? bs = null;
        foreach (var element in value)
        {
            if (element is null)
                throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");

            switch (element)
            {
                case string s:
                    EnsureSetKind(ns, bs, "string");
                    (ss ??= []).Add(s);
                    break;
                case byte[] bytes:
                    EnsureSetKind(ss, ns, "binary");
                    (bs ??= []).Add(DynamoWireValueConversion.CreateBinaryStream(bytes));
                    break;
                default:
                    EnsureSetKind(ss, bs, "number");
                    var attributeValue = DynamoWireValueConversion
                        .ConvertProviderValueToAttributeValue(element);
                    if (attributeValue.N is null)
                        throw new InvalidOperationException(
                            "DynamoDB sets can only contain string, binary, or numeric elements.");
                    (ns ??= []).Add(attributeValue.N);
                    break;
            }
        }

        if (ss is { Count: > 0 })
            return new AttributeValue { SS = ss };
        if (bs is { Count: > 0 })
            return new AttributeValue { BS = bs };
        if (ns is { Count: > 0 })
            return new AttributeValue { NS = ns };

        throw new InvalidOperationException(
            "DynamoDB sets cannot be empty; use a null property or a non-empty collection.");
    }

    /// <summary>Throws when a set would mix DynamoDB set kinds.</summary>
    private static void EnsureSetKind(ICollection? first, ICollection? second, string kind)
    {
        if ((first?.Count ?? 0) > 0 || (second?.Count ?? 0) > 0)
            throw new InvalidOperationException(
                $"DynamoDB sets cannot mix {kind} elements with other element kinds.");
    }

    /// <summary>Serializes a complex scalar list with no element converter.</summary>
    internal static AttributeValue SerializeDirectScalarList(object value, Type elementType)
    {
        if (elementType == typeof(string))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<string>)value);
        if (elementType == typeof(bool))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<bool>)value);
        if (elementType == typeof(bool?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<bool?>)value);
        if (elementType == typeof(byte))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<byte>)value);
        if (elementType == typeof(byte?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<byte?>)value);
        if (elementType == typeof(sbyte))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<sbyte>)value);
        if (elementType == typeof(sbyte?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<sbyte?>)value);
        if (elementType == typeof(short))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<short>)value);
        if (elementType == typeof(short?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<short?>)value);
        if (elementType == typeof(ushort))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<ushort>)value);
        if (elementType == typeof(ushort?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<ushort?>)value);
        if (elementType == typeof(int))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<int>)value);
        if (elementType == typeof(int?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<int?>)value);
        if (elementType == typeof(uint))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<uint>)value);
        if (elementType == typeof(uint?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<uint?>)value);
        if (elementType == typeof(long))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<long>)value);
        if (elementType == typeof(long?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<long?>)value);
        if (elementType == typeof(ulong))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<ulong>)value);
        if (elementType == typeof(ulong?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<ulong?>)value);
        if (elementType == typeof(float))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<float>)value);
        if (elementType == typeof(float?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<float?>)value);
        if (elementType == typeof(double))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<double>)value);
        if (elementType == typeof(double?))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<double?>)value);
        if (elementType == typeof(decimal))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<decimal>)value);
        if (elementType == typeof(decimal?))
            return DynamoAttributeValueCollectionHelpers.SerializeList(
                (IEnumerable<decimal?>)value);
        if (elementType == typeof(byte[]))
            return DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<byte[]>)value);

        return SerializeBoxedScalarList((IEnumerable)value);
    }

    /// <summary>Serializes a complex scalar list using an element converter.</summary>
    internal static AttributeValue SerializeConvertedScalarList(
        object value,
        ValueConverter elementConverter)
    {
        var elements = new List<AttributeValue>();
        foreach (var element in (IEnumerable)value)
        {
            if (element is null)
            {
                elements.Add(new AttributeValue { NULL = true });
                continue;
            }

            var providerValue = elementConverter.ConvertToProvider(element);
            elements.Add(
                providerValue is null
                    ? new AttributeValue { NULL = true }
                    : DynamoWireValueConversion
                        .ConvertProviderValueToAttributeValue(providerValue));
        }

        return new AttributeValue { L = elements };
    }

    /// <summary>Serializes a complex scalar list through boxed scalar elements.</summary>
    private static AttributeValue SerializeBoxedScalarList(IEnumerable value)
    {
        var elements = new List<AttributeValue>();
        foreach (var element in value)
            elements.Add(DynamoWireValueConversion.ConvertProviderValueToAttributeValue(element));
        return new AttributeValue { L = elements };
    }

    /// <summary>Reads a boxed <see cref="KeyValuePair{TKey,TValue}" /> instance.</summary>
    private static (string Key, object? Value) ReadKeyValuePair(object item)
    {
        if (item is KeyValuePair<string, string> stringPair)
            return (stringPair.Key, stringPair.Value);
        if (item is KeyValuePair<string, bool> boolPair)
            return (boolPair.Key, boolPair.Value);
        if (item is KeyValuePair<string, bool?> nullableBoolPair)
            return (nullableBoolPair.Key, nullableBoolPair.Value);
        if (item is KeyValuePair<string, byte> bytePair)
            return (bytePair.Key, bytePair.Value);
        if (item is KeyValuePair<string, byte?> nullableBytePair)
            return (nullableBytePair.Key, nullableBytePair.Value);
        if (item is KeyValuePair<string, sbyte> sbytePair)
            return (sbytePair.Key, sbytePair.Value);
        if (item is KeyValuePair<string, sbyte?> nullableSbytePair)
            return (nullableSbytePair.Key, nullableSbytePair.Value);
        if (item is KeyValuePair<string, short> shortPair)
            return (shortPair.Key, shortPair.Value);
        if (item is KeyValuePair<string, short?> nullableShortPair)
            return (nullableShortPair.Key, nullableShortPair.Value);
        if (item is KeyValuePair<string, ushort> ushortPair)
            return (ushortPair.Key, ushortPair.Value);
        if (item is KeyValuePair<string, ushort?> nullableUshortPair)
            return (nullableUshortPair.Key, nullableUshortPair.Value);
        if (item is KeyValuePair<string, int> intPair)
            return (intPair.Key, intPair.Value);
        if (item is KeyValuePair<string, int?> nullableIntPair)
            return (nullableIntPair.Key, nullableIntPair.Value);
        if (item is KeyValuePair<string, uint> uintPair)
            return (uintPair.Key, uintPair.Value);
        if (item is KeyValuePair<string, uint?> nullableUintPair)
            return (nullableUintPair.Key, nullableUintPair.Value);
        if (item is KeyValuePair<string, long> longPair)
            return (longPair.Key, longPair.Value);
        if (item is KeyValuePair<string, long?> nullableLongPair)
            return (nullableLongPair.Key, nullableLongPair.Value);
        if (item is KeyValuePair<string, ulong> ulongPair)
            return (ulongPair.Key, ulongPair.Value);
        if (item is KeyValuePair<string, ulong?> nullableUlongPair)
            return (nullableUlongPair.Key, nullableUlongPair.Value);
        if (item is KeyValuePair<string, float> floatPair)
            return (floatPair.Key, floatPair.Value);
        if (item is KeyValuePair<string, float?> nullableFloatPair)
            return (nullableFloatPair.Key, nullableFloatPair.Value);
        if (item is KeyValuePair<string, double> doublePair)
            return (doublePair.Key, doublePair.Value);
        if (item is KeyValuePair<string, double?> nullableDoublePair)
            return (nullableDoublePair.Key, nullableDoublePair.Value);
        if (item is KeyValuePair<string, decimal> decimalPair)
            return (decimalPair.Key, decimalPair.Value);
        if (item is KeyValuePair<string, decimal?> nullableDecimalPair)
            return (nullableDecimalPair.Key, nullableDecimalPair.Value);
        if (item is KeyValuePair<string, byte[]> binaryPair)
            return (binaryPair.Key, binaryPair.Value);

        throw new NotSupportedException(
            $"Dictionary item type {item.GetType()} is not supported for DynamoDB wire conversion");
    }
}

/// <summary>Factory interface for closed generic DynamoDB write serializers.</summary>
internal interface IDynamoWriteValueSerializerFactory
{
    /// <summary>Creates a serializer for <typeparamref name="T" />.</summary>
    Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property);
}

/// <summary>Factory interface for closed generic struct DynamoDB write serializers.</summary>
internal interface IDynamoWriteStructValueSerializerFactory
{
    /// <summary>Creates a serializer for <typeparamref name="T" />.</summary>
    Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property) where T : struct;
}
