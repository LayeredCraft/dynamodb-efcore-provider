using System.Collections;
using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Builds AOT-safe scalar write value serializers for untyped EF value edges.</summary>
internal static class DynamoWriteValueSerializerSource
{
    private static readonly ConditionalWeakTable<IProperty, Func<object?, AttributeValue>>
        ScalarValueSerializerCache = new();

    /// <summary>Serializes a scalar property value from an untyped EF edge.</summary>
    internal static AttributeValue SerializeScalarPropertyValue(object? value, IProperty property)
        => GetOrCreateScalarValueSerializer(property)(value);

    /// <summary>Gets or creates the scalar value serializer for a property.</summary>
    internal static Func<object?, AttributeValue>
        GetOrCreateScalarValueSerializer(IProperty property)
        => ScalarValueSerializerCache.GetValue(property, CreateScalarValueSerializer);

    /// <summary>Creates a typed serializer that casts once at the untyped edge.</summary>
    private static Func<object?, AttributeValue> CreateScalarValueSerializer(IProperty property)
    {
        var clrType = property.ClrType;
        var typeMapping = property.GetTypeMapping();
        var propertyConverter = typeMapping.Converter;

        // Property-level converters define the store shape for the entire property, even when the
        // model CLR type itself looks like a collection.
        if (propertyConverter is not null)
            return CreateConvertedPropertySerializer(property, propertyConverter);

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return CreateDirectScalarSerializer(clrType);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(clrType, out var valueType, out _))
        {
            var valueConverter = typeMapping.ElementTypeMapping?.Converter;
            return valueConverter is null
                ? CreateDirectDictionarySerializer(valueType)
                : CreateConvertedDictionarySerializer(valueType, valueConverter);
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? CreateDirectSetSerializer(setElementType)
                : CreateConvertedSetSerializer(setElementType, elementConverter);
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? CreateDirectListSerializer(listElementType)
                : CreateConvertedListSerializer(listElementType, elementConverter);
        }

        return CreateDirectScalarSerializer(clrType);
    }

    private static AttributeValue NullAttributeValue() => new() { NULL = true };

    private interface IObjectValueSerializerFactory
    {
        /// <summary>Creates a serializer for the selected value type.</summary>
        Func<object?, AttributeValue> Create<TValue>();
    }

    private interface IStructObjectValueSerializerFactory
    {
        /// <summary>Creates a serializer for the selected non-nullable value type.</summary>
        Func<object?, AttributeValue> Create<TValue>() where TValue : struct;
    }

    private static Func<object?, AttributeValue>? TryDispatchWireType<TFactory>(
        Type type,
        TFactory factory) where TFactory : struct, IObjectValueSerializerFactory
    {
        if (type == typeof(string))
            return factory.Create<string>();
        if (type == typeof(bool))
            return factory.Create<bool>();
        if (type == typeof(bool?))
            return factory.Create<bool?>();
        if (type == typeof(byte))
            return factory.Create<byte>();
        if (type == typeof(byte?))
            return factory.Create<byte?>();
        if (type == typeof(sbyte))
            return factory.Create<sbyte>();
        if (type == typeof(sbyte?))
            return factory.Create<sbyte?>();
        if (type == typeof(short))
            return factory.Create<short>();
        if (type == typeof(short?))
            return factory.Create<short?>();
        if (type == typeof(ushort))
            return factory.Create<ushort>();
        if (type == typeof(ushort?))
            return factory.Create<ushort?>();
        if (type == typeof(int))
            return factory.Create<int>();
        if (type == typeof(int?))
            return factory.Create<int?>();
        if (type == typeof(uint))
            return factory.Create<uint>();
        if (type == typeof(uint?))
            return factory.Create<uint?>();
        if (type == typeof(long))
            return factory.Create<long>();
        if (type == typeof(long?))
            return factory.Create<long?>();
        if (type == typeof(ulong))
            return factory.Create<ulong>();
        if (type == typeof(ulong?))
            return factory.Create<ulong?>();
        if (type == typeof(float))
            return factory.Create<float>();
        if (type == typeof(float?))
            return factory.Create<float?>();
        if (type == typeof(double))
            return factory.Create<double>();
        if (type == typeof(double?))
            return factory.Create<double?>();
        if (type == typeof(decimal))
            return factory.Create<decimal>();
        if (type == typeof(decimal?))
            return factory.Create<decimal?>();
        if (type == typeof(byte[]))
            return factory.Create<byte[]>();

        return null;
    }

    private static Func<object?, AttributeValue>? TryDispatchModelType<TFactory>(
        Type type,
        TFactory factory) where TFactory : struct, IObjectValueSerializerFactory
    {
        var wireSerializer = TryDispatchWireType(type, factory);
        if (wireSerializer is not null)
            return wireSerializer;

        if (type == typeof(Guid))
            return factory.Create<Guid>();
        if (type == typeof(Guid?))
            return factory.Create<Guid?>();
        if (type == typeof(DateTime))
            return factory.Create<DateTime>();
        if (type == typeof(DateTime?))
            return factory.Create<DateTime?>();
        if (type == typeof(DateTimeOffset))
            return factory.Create<DateTimeOffset>();
        if (type == typeof(DateTimeOffset?))
            return factory.Create<DateTimeOffset?>();
        if (type == typeof(TimeSpan))
            return factory.Create<TimeSpan>();
        if (type == typeof(TimeSpan?))
            return factory.Create<TimeSpan?>();
        if (type == typeof(DateOnly))
            return factory.Create<DateOnly>();
        if (type == typeof(DateOnly?))
            return factory.Create<DateOnly?>();
        if (type == typeof(TimeOnly))
            return factory.Create<TimeOnly>();
        if (type == typeof(TimeOnly?))
            return factory.Create<TimeOnly?>();

        return null;
    }

    private static Func<object?, AttributeValue>? TryDispatchStructModelType<TFactory>(
        Type type,
        TFactory factory) where TFactory : struct, IStructObjectValueSerializerFactory
    {
        if (type == typeof(bool))
            return factory.Create<bool>();
        if (type == typeof(byte))
            return factory.Create<byte>();
        if (type == typeof(sbyte))
            return factory.Create<sbyte>();
        if (type == typeof(short))
            return factory.Create<short>();
        if (type == typeof(ushort))
            return factory.Create<ushort>();
        if (type == typeof(int))
            return factory.Create<int>();
        if (type == typeof(uint))
            return factory.Create<uint>();
        if (type == typeof(long))
            return factory.Create<long>();
        if (type == typeof(ulong))
            return factory.Create<ulong>();
        if (type == typeof(float))
            return factory.Create<float>();
        if (type == typeof(double))
            return factory.Create<double>();
        if (type == typeof(decimal))
            return factory.Create<decimal>();
        if (type == typeof(Guid))
            return factory.Create<Guid>();
        if (type == typeof(DateTime))
            return factory.Create<DateTime>();
        if (type == typeof(DateTimeOffset))
            return factory.Create<DateTimeOffset>();
        if (type == typeof(TimeSpan))
            return factory.Create<TimeSpan>();
        if (type == typeof(DateOnly))
            return factory.Create<DateOnly>();
        if (type == typeof(TimeOnly))
            return factory.Create<TimeOnly>();

        return null;
    }

    private static Func<object?, AttributeValue> CreateDirectScalarSerializer(Type type)
        => TryDispatchWireType(type, default(DirectScalarValueFactory))
            ?? throw new NotSupportedException(
                $"Scalar type '{type.ShortDisplayName()}' is not supported "
                + "for DynamoDB wire conversion.");

    private readonly struct DirectScalarValueFactory : IObjectValueSerializerFactory
    {
        /// <summary>Creates a direct scalar serializer for the selected wire type.</summary>
        public Func<object?, AttributeValue> Create<TValue>()
            => value => value is null
                ? NullAttributeValue()
                : DynamoWireValueConversion.ConvertProviderValueToAttributeValue((TValue)value);
    }

    private static Func<object?, AttributeValue> CreateDirectDictionarySerializer(Type valueType)
        => TryDispatchWireType(valueType, default(DirectDictionaryValueFactory))
            ?? throw new NotSupportedException(
                $"Dictionary value type '{valueType.ShortDisplayName()}' is not supported "
                + "for DynamoDB wire conversion.");

    private readonly struct DirectDictionaryValueFactory : IObjectValueSerializerFactory
    {
        /// <summary>Creates a direct dictionary serializer for the selected value type.</summary>
        public Func<object?, AttributeValue> Create<TValue>()
            => value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                    (IEnumerable<KeyValuePair<string, TValue>>)value);
    }

    private static Func<object?, AttributeValue> CreateDirectSetSerializer(Type elementType)
        => TryDispatchWireType(elementType, default(DirectSetValueFactory))
            ?? throw new NotSupportedException(
                $"Set element type '{elementType.ShortDisplayName()}' is not supported "
                + "for DynamoDB wire conversion.");

    private readonly struct DirectSetValueFactory : IObjectValueSerializerFactory
    {
        /// <summary>Creates a direct set serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>()
            => value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeSet((IEnumerable<TElement>)value);
    }

    private static Func<object?, AttributeValue> CreateDirectListSerializer(Type elementType)
        => TryDispatchWireType(elementType, default(DirectListValueFactory))
            ?? throw new NotSupportedException(
                $"List element type '{elementType.ShortDisplayName()}' is not supported "
                + "for DynamoDB wire conversion.");

    private readonly struct DirectListValueFactory : IObjectValueSerializerFactory
    {
        /// <summary>Creates a direct list serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>()
            => value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeList((IEnumerable<TElement>)value);
    }

    private static Func<object?, AttributeValue> CreateConvertedPropertySerializer(
        IProperty property,
        ValueConverter converter)
    {
        var modelType = converter.ModelClrType;
        var propertyType = property.ClrType;
        var providerType = converter.ProviderClrType;

        if (propertyType == modelType)
            return TryDispatchModelType(
                    modelType,
                    new ConvertedPropertyValueFactory(converter, providerType))
                ?? CreateBoxedConvertedPropertySerializer(converter);

        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying == modelType)
            return TryDispatchStructModelType(
                    modelType,
                    new ConvertedNullablePropertyValueFactory(converter, providerType))
                ?? CreateBoxedConvertedPropertySerializer(converter);

        return CreateBoxedConvertedPropertySerializer(converter);
    }

    private readonly struct ConvertedPropertyValueFactory(
        ValueConverter converter,
        Type providerType) : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted scalar serializer for the selected model type.</summary>
        public Func<object?, AttributeValue> Create<TModel>()
            => TryDispatchWireType(
                    providerType,
                    new ConvertedPropertyProviderFactory<TModel>(converter))
                ?? CreateBoxedConvertedPropertySerializer(converter);
    }

    private readonly struct ConvertedPropertyProviderFactory<TModel>(ValueConverter converter)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted scalar serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TModel, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    typed.ConvertToProviderTyped((TModel)value));
        }
    }

    private readonly struct ConvertedNullablePropertyValueFactory(
        ValueConverter converter,
        Type providerType) : IStructObjectValueSerializerFactory
    {
        /// <summary>Creates a converted nullable scalar serializer for the selected model type.</summary>
        public Func<object?, AttributeValue> Create<TModel>() where TModel : struct
            => TryDispatchWireType(
                    providerType,
                    new ConvertedNullablePropertyProviderFactory<TModel>(converter))
                ?? CreateBoxedConvertedPropertySerializer(converter);
    }

    private readonly struct ConvertedNullablePropertyProviderFactory<TModel>(
        ValueConverter converter) : IObjectValueSerializerFactory where TModel : struct
    {
        /// <summary>Creates a converted nullable scalar serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TModel, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    typed.ConvertToProviderTyped((TModel)value));
        }
    }

    private static Func<object?, AttributeValue> CreateBoxedConvertedPropertySerializer(
        ValueConverter converter)
        => value => value is null
            ? NullAttributeValue()
            : ConvertProviderShapeToAttributeValue(converter.ConvertToProvider(value));

    private static Func<object?, AttributeValue> CreateConvertedDictionarySerializer(
        Type valueType,
        ValueConverter converter)
    {
        var modelType = converter.ModelClrType;
        var providerType = converter.ProviderClrType;

        if (valueType == modelType)
            return TryDispatchModelType(
                    modelType,
                    new ConvertedDictionaryValueFactory(converter, providerType))
                ?? CreateBoxedConvertedDictionarySerializer(converter);

        var underlying = Nullable.GetUnderlyingType(valueType);
        if (underlying == modelType)
            return TryDispatchStructModelType(
                    modelType,
                    new ConvertedNullableDictionaryValueFactory(converter, providerType))
                ?? CreateBoxedConvertedDictionarySerializer(converter);

        return CreateBoxedConvertedDictionarySerializer(converter);
    }

    private readonly struct ConvertedDictionaryValueFactory(
        ValueConverter converter,
        Type providerType) : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted dictionary serializer for the selected value type.</summary>
        public Func<object?, AttributeValue> Create<TValue>()
            => TryDispatchWireType(
                    providerType,
                    new ConvertedDictionaryProviderFactory<TValue>(converter))
                ?? CreateBoxedConvertedDictionarySerializer(converter);
    }

    private readonly struct ConvertedDictionaryProviderFactory<TValue>(ValueConverter converter)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted dictionary serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TValue, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                    (IEnumerable<KeyValuePair<string, TValue>>)value,
                    typed.ConvertToProviderTyped);
        }
    }

    private readonly struct ConvertedNullableDictionaryValueFactory(
        ValueConverter converter,
        Type providerType) : IStructObjectValueSerializerFactory
    {
        /// <summary>Creates a converted nullable dictionary serializer for the selected value type.</summary>
        public Func<object?, AttributeValue> Create<TValue>() where TValue : struct
            => TryDispatchWireType(
                    providerType,
                    new ConvertedNullableDictionaryProviderFactory<TValue>(converter))
                ?? CreateBoxedConvertedDictionarySerializer(converter);
    }

    private readonly struct ConvertedNullableDictionaryProviderFactory<TValue>(
        ValueConverter converter) : IObjectValueSerializerFactory where TValue : struct
    {
        /// <summary>Creates a converted nullable dictionary serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TValue, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                    (IEnumerable<KeyValuePair<string, TValue?>>)value,
                    item => item.HasValue
                        ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                            typed.ConvertToProviderTyped(item.Value))
                        : NullAttributeValue());
        }
    }

    private static Func<object?, AttributeValue> CreateBoxedConvertedDictionarySerializer(
        ValueConverter converter)
        => value =>
        {
            if (value is null)
                return NullAttributeValue();

            if (value is IDictionary dictionary)
            {
                var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
                foreach (DictionaryEntry item in dictionary)
                {
                    var providerValue =
                        item.Value is null ? null : converter.ConvertToProvider(item.Value);
                    map[(string)item.Key] = ConvertProviderShapeToAttributeValue(providerValue);
                }

                return new AttributeValue { M = map };
            }

            if (FindStringKeyValuePairEnumerable(value.GetType()) is not null)
                return SerializeBoxedModelMap((IEnumerable)value, converter);

            throw new NotSupportedException(
                "Converted dictionary fallback requires a dictionary with string keys.");
        };

    private static Func<object?, AttributeValue> CreateConvertedSetSerializer(
        Type elementType,
        ValueConverter converter)
    {
        var modelType = converter.ModelClrType;
        var providerType = converter.ProviderClrType;

        if (elementType == modelType)
            return TryDispatchModelType(
                    modelType,
                    new ConvertedSetValueFactory(converter, providerType))
                ?? CreateBoxedConvertedSetSerializer(converter);

        var underlying = Nullable.GetUnderlyingType(elementType);
        if (underlying == modelType)
            return TryDispatchStructModelType(
                    modelType,
                    new ConvertedNullableSetValueFactory(converter, providerType))
                ?? CreateBoxedConvertedSetSerializer(converter);

        return CreateBoxedConvertedSetSerializer(converter);
    }

    private readonly struct ConvertedSetValueFactory(ValueConverter converter, Type providerType)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted set serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>()
            => TryDispatchWireType(
                    providerType,
                    new ConvertedSetProviderFactory<TElement>(converter))
                ?? CreateBoxedConvertedSetSerializer(converter);
    }

    private readonly struct ConvertedSetProviderFactory<TElement>(ValueConverter converter)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted set serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeSet(
                    (IEnumerable<TElement>)value,
                    typed.ConvertToProviderTyped);
        }
    }

    private readonly struct ConvertedNullableSetValueFactory(
        ValueConverter converter,
        Type providerType) : IStructObjectValueSerializerFactory
    {
        /// <summary>Creates a converted nullable set serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>() where TElement : struct
            => TryDispatchWireType(
                    providerType,
                    new ConvertedNullableSetProviderFactory<TElement>(converter))
                ?? CreateBoxedConvertedSetSerializer(converter);
    }

    private readonly struct ConvertedNullableSetProviderFactory<TElement>(ValueConverter converter)
        : IObjectValueSerializerFactory where TElement : struct
    {
        /// <summary>Creates a converted nullable set serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeSet(
                    (IEnumerable<TElement?>)value,
                    item => item.HasValue
                        ? typed.ConvertToProviderTyped(item.Value)
                        : throw new InvalidOperationException(
                            "DynamoDB sets cannot contain null elements."));
        }
    }

    private static Func<object?, AttributeValue> CreateBoxedConvertedSetSerializer(
        ValueConverter converter)
        => value =>
        {
            if (value is null)
                return NullAttributeValue();

            var providerValues = new List<object>();
            foreach (var element in (IEnumerable)value)
            {
                if (element is null)
                    throw new InvalidOperationException(
                        "DynamoDB sets cannot contain null elements.");

                var providerValue = converter.ConvertToProvider(element);
                if (providerValue is null)
                    throw new InvalidOperationException(
                        "DynamoDB sets cannot contain null elements.");

                providerValues.Add(providerValue);
            }

            return SerializeBoxedScalarSet(providerValues);
        };

    private static Func<object?, AttributeValue> CreateConvertedListSerializer(
        Type elementType,
        ValueConverter converter)
    {
        var modelType = converter.ModelClrType;
        var providerType = converter.ProviderClrType;

        if (elementType == modelType)
            return TryDispatchModelType(
                    modelType,
                    new ConvertedListValueFactory(converter, providerType))
                ?? CreateBoxedConvertedListSerializer(converter);

        var underlying = Nullable.GetUnderlyingType(elementType);
        if (underlying == modelType)
            return TryDispatchStructModelType(
                    modelType,
                    new ConvertedNullableListValueFactory(converter, providerType))
                ?? CreateBoxedConvertedListSerializer(converter);

        return CreateBoxedConvertedListSerializer(converter);
    }

    private readonly struct ConvertedListValueFactory(ValueConverter converter, Type providerType)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted list serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>()
            => TryDispatchWireType(
                    providerType,
                    new ConvertedListProviderFactory<TElement>(converter))
                ?? CreateBoxedConvertedListSerializer(converter);
    }

    private readonly struct ConvertedListProviderFactory<TElement>(ValueConverter converter)
        : IObjectValueSerializerFactory
    {
        /// <summary>Creates a converted list serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeList(
                    (IEnumerable<TElement>)value,
                    typed.ConvertToProviderTyped);
        }
    }

    private readonly struct ConvertedNullableListValueFactory(
        ValueConverter converter,
        Type providerType) : IStructObjectValueSerializerFactory
    {
        /// <summary>Creates a converted nullable list serializer for the selected element type.</summary>
        public Func<object?, AttributeValue> Create<TElement>() where TElement : struct
            => TryDispatchWireType(
                    providerType,
                    new ConvertedNullableListProviderFactory<TElement>(converter))
                ?? CreateBoxedConvertedListSerializer(converter);
    }

    private readonly struct ConvertedNullableListProviderFactory<TElement>(ValueConverter converter)
        : IObjectValueSerializerFactory where TElement : struct
    {
        /// <summary>Creates a converted nullable list serializer for the selected provider type.</summary>
        public Func<object?, AttributeValue> Create<TProvider>()
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return value => value is null
                ? NullAttributeValue()
                : DynamoAttributeValueCollectionHelpers.SerializeList(
                    (IEnumerable<TElement?>)value,
                    item => item.HasValue
                        ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                            typed.ConvertToProviderTyped(item.Value))
                        : NullAttributeValue());
        }
    }

    private static Func<object?, AttributeValue> CreateBoxedConvertedListSerializer(
        ValueConverter converter)
        => value =>
        {
            if (value is null)
                return NullAttributeValue();

            var elements = new List<AttributeValue>();
            foreach (var element in (IEnumerable)value)
            {
                var providerValue = element is null ? null : converter.ConvertToProvider(element);
                elements.Add(ConvertProviderShapeToAttributeValue(providerValue));
            }

            return new AttributeValue { L = elements };
        };

    /// <summary>Serializes a provider-shaped scalar, list, or map value from a boxed fallback.</summary>
    private static AttributeValue ConvertProviderShapeToAttributeValue(object? providerValue)
    {
        if (providerValue is null)
            return NullAttributeValue();

        if (providerValue is string or byte[])
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(providerValue);

        if (providerValue is IDictionary dictionary)
            return SerializeBoxedProviderMap(dictionary);

        if (FindStringKeyValuePairEnumerable(providerValue.GetType()) is not null)
            return SerializeBoxedProviderMap((IEnumerable)providerValue);

        if (providerValue is IEnumerable enumerable)
            return SerializeBoxedScalarList(enumerable);

        return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(providerValue);
    }

    /// <summary>Finds an enumerable string-key dictionary contract on a provider-shaped value.</summary>
    private static Type? FindStringKeyValuePairEnumerable(Type type)
    {
        foreach (var candidate in type.IsInterface
            ? [type, .. type.GetInterfaces()]
            : type.GetInterfaces())
        {
            if (!candidate.IsGenericType
                || candidate.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                continue;

            var elementType = candidate.GetGenericArguments()[0];
            if (!elementType.IsGenericType
                || elementType.GetGenericTypeDefinition() != typeof(KeyValuePair<,>)
                || elementType.GetGenericArguments()[0] != typeof(string))
                continue;

            return candidate;
        }

        return null;
    }

    /// <summary>Serializes generic model dictionary entries through a boxed converter fallback.</summary>
    private static AttributeValue SerializeBoxedModelMap(
        IEnumerable value,
        ValueConverter converter)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var item in value)
        {
            var type = item?.GetType()
                ?? throw new InvalidOperationException(
                    "DynamoDB maps cannot contain null entries.");
            var key = (string)type.GetProperty("Key")!.GetValue(item)!;
            var modelValue = type.GetProperty("Value")!.GetValue(item);
            var providerValue = modelValue is null ? null : converter.ConvertToProvider(modelValue);
            map[key] = ConvertProviderShapeToAttributeValue(providerValue);
        }

        return new AttributeValue { M = map };
    }

    /// <summary>Serializes provider-shaped dictionary entries from a boxed fallback.</summary>
    private static AttributeValue SerializeBoxedProviderMap(IDictionary value)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in value)
            map[(string)item.Key] = ConvertProviderShapeToAttributeValue(item.Value);
        return new AttributeValue { M = map };
    }

    /// <summary>Serializes provider-shaped generic dictionary entries from a boxed fallback.</summary>
    private static AttributeValue SerializeBoxedProviderMap(IEnumerable value)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var item in value)
        {
            var type = item?.GetType()
                ?? throw new InvalidOperationException(
                    "DynamoDB maps cannot contain null entries.");
            var key = (string)type.GetProperty("Key")!.GetValue(item)!;
            var providerValue = type.GetProperty("Value")!.GetValue(item);
            map[key] = ConvertProviderShapeToAttributeValue(providerValue);
        }

        return new AttributeValue { M = map };
    }

    /// <summary>Serializes scalar set elements through boxed provider values.</summary>
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

    /// <summary>Serializes a complex scalar list through boxed scalar elements.</summary>
    private static AttributeValue SerializeBoxedScalarList(IEnumerable value)
    {
        var elements = new List<AttributeValue>();
        foreach (var element in value)
            elements.Add(ConvertProviderShapeToAttributeValue(element));
        return new AttributeValue { L = elements };
    }
}
