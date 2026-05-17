using System.Collections;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API usage

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoEntityWritePlanFactory
{
    /// <summary>Builds a write plan for the given entity type.</summary>
    public EntityWritePlan BuildPlan(
        IEntityType entityType,
        Func<IProperty, Func<IUpdateEntry, AttributeValue>> createPropertySerializer)
    {
        var properties = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            // Runtime-only properties are populated by query/runtime pipelines and must never be
            // serialized to DynamoDB.
            .Where(static p => !p.IsRuntimeOnly())
            .ToList();

        var propertyWriters = new List<PropertyWriteAction>(properties.Count);
        var propertySerializers =
            new Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>>(
                properties.Count,
                ReferenceEqualityComparer.Instance);

        foreach (var p in properties)
        {
            var serializer = createPropertySerializer(p);
            propertyWriters.Add(new PropertyWriteAction(p.GetAttributeName(), serializer));
            propertySerializers[p] = serializer;
        }

        var complexProperties = entityType.GetComplexProperties().ToList();

        return new EntityWritePlan(propertyWriters, propertySerializers, complexProperties);
    }
}

internal readonly record struct PropertyWriteAction(
    string AttributeName,
    Func<IUpdateEntry, AttributeValue> Serialize);

internal sealed class EntityWritePlan(
    List<PropertyWriteAction> propertyWriters,
    Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>> propertySerializers,
    List<IComplexProperty> complexProperties)
{
    /// <summary>Serializes the current value of a single tracked property for the update path.</summary>
    internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
    {
        if (!propertySerializers.TryGetValue(property, out var serializer))
            throw new InvalidOperationException(
                $"No serializer was built for property "
                + $"'{property.DeclaringType?.DisplayName()}.{property.Name}'. "
                + "Shadow key properties are not serialized.");

        return serializer(entry);
    }

    /// <summary>Serializes the full entity entry into a DynamoDB item dictionary.</summary>
    public Dictionary<string, AttributeValue> Serialize(IUpdateEntry entry)
    {
        var result = new Dictionary<string, AttributeValue>(
            propertyWriters.Count + complexProperties.Count,
            StringComparer.Ordinal);

        foreach (var writer in propertyWriters)
            result[writer.AttributeName] = writer.Serialize(entry);

        if (complexProperties.Count == 0)
            return result;

        // Complex type values are embedded in the owning entity — read via the CLR getter
        // rather than the change tracker, which does not track complex types separately.
        var entity = ((InternalEntityEntry)entry).Entity;

        foreach (var cp in complexProperties)
        {
            var value = cp.GetGetter().GetClrValue(entity);
            if (value is null && !cp.IsCollection)
                continue;

            result[((IReadOnlyComplexProperty)cp).GetAttributeName()] =
                SerializeComplexProperty(value, cp);
        }

        return result;
    }

    /// <summary>
    ///     Serializes a complex property value to an <see cref="AttributeValue" /> (M map or L list).
    ///     Also used by the PartiQL UPDATE path when building SET clause parameters.
    /// </summary>
    internal static AttributeValue SerializeComplexProperty(object? value, IComplexProperty cp)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        if (cp.IsCollection)
        {
            var elements = new List<AttributeValue>();
            foreach (var element in (IEnumerable)value)
            {
                if (element is null)
                    throw new InvalidOperationException(
                        $"Complex collection '{cp.DeclaringType.DisplayName()}.{cp.Name}' "
                        + "contains null element. Elements must be non-null complex objects.");

                var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
                SerializeComplexTypeIntoMap(element, cp.ComplexType, map);
                elements.Add(new AttributeValue { M = map });
            }

            return new AttributeValue { L = elements };
        }
        else
        {
            var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
            SerializeComplexTypeIntoMap(value, cp.ComplexType, map);
            return new AttributeValue { M = map };
        }
    }

    /// <summary>Recursively serializes a complex type instance into a DynamoDB attribute map.</summary>
    private static void SerializeComplexTypeIntoMap(
        object instance,
        IComplexType complexType,
        Dictionary<string, AttributeValue> map)
    {
        foreach (var property in complexType.GetProperties())
        {
            if (property.IsRuntimeOnly())
                continue;

            var rawValue = property.GetGetter().GetClrValue(instance);
            map[property.GetAttributeName()] = SerializeScalarPropertyValue(rawValue, property);
        }

        foreach (var nestedCp in complexType.GetComplexProperties())
        {
            var nestedValue = nestedCp.GetGetter().GetClrValue(instance);
            map[((IReadOnlyComplexProperty)nestedCp).GetAttributeName()] =
                SerializeComplexProperty(nestedValue, nestedCp);
        }
    }

    /// <summary>
    ///     Serializes a single scalar property value using the property's type mapping. Used only on
    ///     the complex-type path; root entity scalar properties use typed delegates compiled at
    ///     plan-build time.
    /// </summary>
    internal static AttributeValue SerializeScalarPropertyValue(object? value, IProperty property)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        var typeMapping = property.GetTypeMapping();
        var converter = typeMapping.Converter;

        // Property-level converters define the store shape for the entire property, even when the
        // model CLR type itself looks like a collection.
        if (converter is not null)
        {
            var providerValue = converter.ConvertToProvider(value);
            return ConvertProviderShapeToAttributeValue(providerValue);
        }

        var clrType = property.ClrType;
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue((byte[])value);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out _))
        {
            var valueConverter = typeMapping.ElementTypeMapping?.Converter;
            return valueConverter is null
                ? SerializeDirectScalarDictionary(value, dictionaryValueType)
                : SerializeConvertedScalarDictionary(value, valueConverter);
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? SerializeDirectScalarSet(value, setElementType)
                : SerializeConvertedScalarSet(value, elementConverter);
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? SerializeDirectScalarList(value, listElementType)
                : SerializeConvertedScalarList(value, elementConverter);
        }

        return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(value);
    }

    /// <summary>Serializes a provider-shaped scalar or list value.</summary>
    private static AttributeValue ConvertProviderShapeToAttributeValue(object? providerValue)
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
    private static AttributeValue SerializeDirectScalarDictionary(object value, Type valueType)
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
    private static AttributeValue SerializeConvertedScalarDictionary(
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
    private static AttributeValue SerializeDirectScalarSet(object value, Type elementType)
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
    private static AttributeValue SerializeConvertedScalarSet(
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
    private static AttributeValue SerializeDirectScalarList(object value, Type elementType)
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
    private static AttributeValue SerializeConvertedScalarList(
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
