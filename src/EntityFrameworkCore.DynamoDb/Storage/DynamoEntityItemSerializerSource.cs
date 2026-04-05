using System.Collections;
using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Builds and caches typed write plans per <see cref="IEntityType"/> for SaveChanges.
/// </summary>
public sealed class DynamoEntityItemSerializerSource
{
    private readonly ConcurrentDictionary<IEntityType, EntityWritePlan> _cache = new();

    /// <summary>
    /// Returns the fully assembled DynamoDB item dictionary for a root
    /// <see cref="IUpdateEntry"/> and its owned sub-entries.
    /// </summary>
    public Dictionary<string, AttributeValue> BuildItem(
        IUpdateEntry rootEntry,
        IReadOnlyDictionary<object, IUpdateEntry> ownedEntries)
        => GetOrBuildPlan(rootEntry.EntityType).Serialize(rootEntry, ownedEntries, this);

    /// <summary>Returns the fully assembled DynamoDB item dictionary for an owned sub-entry.</summary>
    internal Dictionary<string, AttributeValue> BuildItemFromOwnedEntry(
        IUpdateEntry entry,
        IReadOnlyDictionary<object, IUpdateEntry> ownedEntries)
        => GetOrBuildPlan(entry.EntityType).Serialize(entry, ownedEntries, this);

    private EntityWritePlan GetOrBuildPlan(IEntityType entityType)
        => _cache.GetOrAdd(entityType, BuildPlan);

    private static EntityWritePlan BuildPlan(IEntityType entityType)
    {
        var propertyWriters = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            .Select(CreatePropertyWriter)
            .ToList();

        var ownedNavigations = entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned())
            .ToList();

        return new EntityWritePlan(propertyWriters, ownedNavigations);
    }

    private static PropertyWriteAction CreatePropertyWriter(IProperty property)
        => new(property.GetAttributeName(), CreatePropertySerializer(property));

    private static Func<IUpdateEntry, AttributeValue> CreatePropertySerializer(IProperty property)
    {
        var clrType = property.ClrType;
        var typeMapping = property.GetTypeMapping();
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (nonNullableType == typeof(byte[]))
            return CreateScalarPropertySerializer(property, clrType, typeMapping.Converter);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(clrType, out var valueType, out _))
            return CreateDictionaryPropertySerializer(
                property,
                valueType,
                typeMapping.ElementTypeMapping?.Converter);

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
            return CreateSetPropertySerializer(
                property,
                setElementType,
                typeMapping.ElementTypeMapping?.Converter);

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
            return CreateListPropertySerializer(
                property,
                listElementType,
                typeMapping.ElementTypeMapping?.Converter);

        return CreateScalarPropertySerializer(property, clrType, typeMapping.Converter);
    }

    private static Func<IUpdateEntry, AttributeValue> CreateScalarPropertySerializer(
        IProperty property,
        Type propertyType,
        ValueConverter? converter)
        => converter == null
            ? CreateDirectScalarPropertySerializer(property, propertyType)
            : CreateConvertedScalarPropertySerializer(property, converter);

    private static Func<IUpdateEntry, AttributeValue> CreateListPropertySerializer(
        IProperty property,
        Type elementType,
        ValueConverter? converter)
        => converter == null
            ? CreateDirectListPropertySerializer(property, elementType)
            : CreateConvertedListPropertySerializer(property, converter);

    private static Func<IUpdateEntry, AttributeValue> CreateSetPropertySerializer(
        IProperty property,
        Type elementType,
        ValueConverter? converter)
        => converter == null
            ? CreateDirectSetPropertySerializer(property, elementType)
            : CreateConvertedSetPropertySerializer(property, converter);

    private static Func<IUpdateEntry, AttributeValue> CreateDictionaryPropertySerializer(
        IProperty property,
        Type valueType,
        ValueConverter? converter)
        => converter == null
            ? CreateDirectDictionaryPropertySerializer(property, valueType)
            : CreateConvertedDictionaryPropertySerializer(property, converter);

    private static Func<IUpdateEntry, AttributeValue> CreateDirectScalarPropertySerializer(
        IProperty property,
        Type propertyType)
        => DispatchSupportedDirectType(property, propertyType, default(DirectScalarFactory));

    private static Func<IUpdateEntry, AttributeValue> CreateDirectListPropertySerializer(
        IProperty property,
        Type elementType)
        => DispatchSupportedDirectType(property, elementType, default(DirectListFactory));

    private static Func<IUpdateEntry, AttributeValue> CreateDirectSetPropertySerializer(
        IProperty property,
        Type elementType)
        => DispatchSupportedDirectType(property, elementType, default(DirectSetFactory));

    private static Func<IUpdateEntry, AttributeValue> CreateDirectDictionaryPropertySerializer(
        IProperty property,
        Type valueType)
        => DispatchSupportedDirectType(property, valueType, default(DirectDictionaryFactory));

    /// <summary>
    /// Creates a write delegate for a scalar property with a value converter. Dispatches on the
    /// converter's provider type at plan-build time so the hot path uses a typed cast and
    /// JIT-specialized <see cref="DynamoWireValueConversion.ConvertProviderValueToAttributeValue{T}"/>
    /// rather than a boxed pattern-match dispatch at every write.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CreateConvertedScalarPropertySerializer(
        IProperty property,
        ValueConverter converter)
    {
        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchSupportedDirectType(
            property,
            converter.ProviderClrType,
            new ConvertedScalarFactory(converter));
    }

    /// <summary>
    /// Creates a write delegate for a list property with an element-level converter. Dispatches on
    /// the converter's provider type at plan-build time to avoid per-element pattern-match dispatch.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CreateConvertedListPropertySerializer(
        IProperty property,
        ValueConverter converter)
    {
        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchSupportedDirectType(
            property,
            converter.ProviderClrType,
            new ConvertedListFactory(converter));
    }

    /// <summary>
    /// Creates a write delegate for a set property with an element-level converter. Dispatches on
    /// the converter's provider type at plan-build time so the write loop directly accumulates
    /// SS/NS/BS without creating and destructuring intermediate <see cref="AttributeValue"/> objects.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CreateConvertedSetPropertySerializer(
        IProperty property,
        ValueConverter converter)
    {
        EnsureSupportedSetProviderType(property, converter.ProviderClrType);
        return DispatchSupportedDirectType(
            property,
            converter.ProviderClrType,
            new ConvertedSetFactory(converter));
    }

    /// <summary>
    /// Creates a write delegate for a dictionary property with a value-level converter. Dispatches
    /// on the converter's provider type at plan-build time.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CreateConvertedDictionaryPropertySerializer(
        IProperty property,
        ValueConverter converter)
    {
        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchSupportedDirectType(
            property,
            converter.ProviderClrType,
            new ConvertedDictionaryFactory(converter));
    }

    private static void EnsureSupportedValueProviderType(IProperty property, Type providerType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(providerType) ?? providerType;
        if (DynamoTypeMappingSource.IsPrimitiveType(nonNullableType))
            return;

        throw new NotSupportedException(
            $"Property '{property.DeclaringType.DisplayName()}.{property.Name}' has unsupported "
            + $"converter provider type '{providerType.ShortDisplayName()}' on the write path.");
    }

    private static void EnsureSupportedSetProviderType(IProperty property, Type providerType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(providerType) ?? providerType;
        if (nonNullableType == typeof(string)
            || nonNullableType == typeof(byte[])
            || IsNumericType(nonNullableType))
            return;

        throw new NotSupportedException(
            $"Set property '{property.DeclaringType.DisplayName()}.{property.Name}' has unsupported "
            + $"converter provider type '{providerType.ShortDisplayName()}' on the write path.");
    }

    private static Exception CreateCollectionRuntimeTypeException(
        IProperty property,
        Type runtimeType,
        string expectedInterface)
        => new NotSupportedException(
            $"Property '{property.DeclaringType.DisplayName()}.{property.Name}' has runtime type "
            + $"'{runtimeType.ShortDisplayName()}' which does not implement '{expectedInterface}'.");

    private static bool IsNumericType(Type type)
        => type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);

    private static Func<IUpdateEntry, AttributeValue> DispatchSupportedDirectType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, IDirectSerializerFactory
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

        throw new NotSupportedException(
            $"Property '{property.DeclaringType.DisplayName()}.{property.Name}' has unsupported "
            + $"provider-native type '{type.ShortDisplayName()}' on the write path.");
    }

    private static Func<IUpdateEntry, AttributeValue> CreateDirectScalarSerializer<TProperty>(
        IProperty property)
        => entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
            entry.GetCurrentValue<TProperty>(property));

    private static Func<IUpdateEntry, AttributeValue>
        CreateDirectListSerializer<TElement>(IProperty property)
        => entry => SerializeCollectionOrNull(
            entry.GetCurrentValue<IEnumerable<TElement>>(property),
            static value => DynamoAttributeValueCollectionHelpers
                .SerializeList<IEnumerable<TElement>, TElement>(value));

    private static Func<IUpdateEntry, AttributeValue>
        CreateDirectSetSerializer<TElement>(IProperty property)
        => entry => SerializeCollectionOrNull(
            entry.GetCurrentValue<IEnumerable<TElement>>(property),
            static value => DynamoAttributeValueCollectionHelpers
                .SerializeSet<IEnumerable<TElement>, TElement>(value));

    private static Func<IUpdateEntry, AttributeValue>
        CreateDirectDictionarySerializer<TValue>(IProperty property)
        => entry => SerializeCollectionOrNull(
            entry.GetCurrentValue<IEnumerable<KeyValuePair<string, TValue>>>(property),
            static value => DynamoAttributeValueCollectionHelpers
                .SerializeDictionary<IEnumerable<KeyValuePair<string, TValue>>, TValue>(value));

    private static AttributeValue SerializeCollectionOrNull<TCollection>(
        TCollection value,
        Func<TCollection, AttributeValue> serialize)
        => value is null ? CreateNullAttributeValue() : serialize(value);

    private static AttributeValue CreateNullAttributeValue() => new() { NULL = true };

    private interface IDirectSerializerFactory
    {
        Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property);
    }

    private readonly struct DirectScalarFactory : IDirectSerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => CreateDirectScalarSerializer<T>(property);
    }

    private readonly struct DirectListFactory : IDirectSerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => CreateDirectListSerializer<T>(property);
    }

    private readonly struct DirectSetFactory : IDirectSerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => CreateDirectSetSerializer<T>(property);
    }

    private readonly struct DirectDictionaryFactory : IDirectSerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => CreateDirectDictionarySerializer<T>(property);
    }

    /// <summary>
    ///     Converter-path factory for scalar properties. Dispatches on the provider type at
    ///     plan-build time so the write delegate uses a typed cast and JIT-specialized conversion rather
    ///     than a boxed pattern-match dispatch on every write.
    /// </summary>
    private readonly struct ConvertedScalarFactory : IDirectSerializerFactory
    {
        private readonly ValueConverter _converter;

        internal ConvertedScalarFactory(ValueConverter converter) => _converter = converter;

        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            // Copy to local so the lambda captures a reference rather than this struct.
            var conv = _converter;
            return entry =>
            {
                var providerValue = conv.ConvertToProvider(entry.GetCurrentValue(property));
                if (providerValue is null)
                    return CreateNullAttributeValue();
                return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    (TProvider)providerValue);
            };
        }
    }

    /// <summary>
    ///     Converter-path factory for list properties. Delegates to
    ///     <see
    ///         cref="DynamoAttributeValueCollectionHelpers.SerializeList{TProvider}(IEnumerable,Func{object?,object?})" />
    ///     which passes converter results directly into the typed element serializer.
    /// </summary>
    private readonly struct ConvertedListFactory : IDirectSerializerFactory
    {
        private readonly ValueConverter _converter;

        internal ConvertedListFactory(ValueConverter converter) => _converter = converter;

        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = _converter;
            return entry =>
            {
                var value = entry.GetCurrentValue(property);
                if (value is null)
                    return CreateNullAttributeValue();
                if (value is not IEnumerable enumerable)
                    throw CreateCollectionRuntimeTypeException(
                        property,
                        value.GetType(),
                        nameof(IEnumerable));
                return DynamoAttributeValueCollectionHelpers.SerializeList<TProvider>(
                    enumerable,
                    conv.ConvertToProvider);
            };
        }
    }

    /// <summary>
    ///     Converter-path factory for set properties. Delegates to
    ///     <see
    ///         cref="DynamoAttributeValueCollectionHelpers.SerializeSet{TProvider}(IEnumerable,Func{object?,object?})" />
    ///     which accumulates directly into SS/NS/BS without creating intermediate
    ///     <see cref="AttributeValue" /> objects.
    /// </summary>
    private readonly struct ConvertedSetFactory : IDirectSerializerFactory
    {
        private readonly ValueConverter _converter;

        internal ConvertedSetFactory(ValueConverter converter) => _converter = converter;

        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = _converter;
            return entry =>
            {
                var value = entry.GetCurrentValue(property);
                if (value is null)
                    return CreateNullAttributeValue();
                if (value is not IEnumerable enumerable)
                    throw CreateCollectionRuntimeTypeException(
                        property,
                        value.GetType(),
                        nameof(IEnumerable));
                return DynamoAttributeValueCollectionHelpers.SerializeSet<TProvider>(
                    enumerable,
                    conv.ConvertToProvider);
            };
        }
    }

    /// <summary>
    ///     Converter-path factory for dictionary properties. Delegates to
    ///     <see
    ///         cref="DynamoAttributeValueCollectionHelpers.SerializeDictionary{TProvider}(IDictionary,Func{object?,object?})" />
    ///     which passes converter results directly into the typed value serializer.
    /// </summary>
    private readonly struct ConvertedDictionaryFactory : IDirectSerializerFactory
    {
        private readonly ValueConverter _converter;

        internal ConvertedDictionaryFactory(ValueConverter converter) => _converter = converter;

        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = _converter;
            return entry =>
            {
                var value = entry.GetCurrentValue(property);
                if (value is null)
                    return CreateNullAttributeValue();
                if (value is not IDictionary dictionary)
                    throw CreateCollectionRuntimeTypeException(
                        property,
                        value.GetType(),
                        nameof(IDictionary));
                return DynamoAttributeValueCollectionHelpers.SerializeDictionary<TProvider>(
                    dictionary,
                    conv.ConvertToProvider);
            };
        }
    }

    private readonly record struct PropertyWriteAction(
        string AttributeName,
        Func<IUpdateEntry, AttributeValue> Serialize);

    private sealed class EntityWritePlan(
        List<PropertyWriteAction> propertyWriters,
        List<INavigation> ownedNavigations)
    {
        public Dictionary<string, AttributeValue> Serialize(
            IUpdateEntry entry,
            IReadOnlyDictionary<object, IUpdateEntry> ownedEntries,
            DynamoEntityItemSerializerSource source)
        {
            var result = new Dictionary<string, AttributeValue>(
                propertyWriters.Count + ownedNavigations.Count,
                StringComparer.Ordinal);

            foreach (var writer in propertyWriters)
                result[writer.AttributeName] = writer.Serialize(entry);

            foreach (var nav in ownedNavigations)
            {
                var navValue = entry.GetCurrentValue(nav);
                if (navValue is null)
                    continue;

                var attributeName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;

                if (nav.IsCollection)
                {
                    var elements = new List<AttributeValue>();
                    if (navValue is IEnumerable collection)
                    {
                        foreach (var element in collection)
                        {
                            if (element is not null
                                && ownedEntries.TryGetValue(element, out var ownedEntry))
                                elements.Add(
                                    new AttributeValue
                                    {
                                        M = source.BuildItemFromOwnedEntry(
                                            ownedEntry,
                                            ownedEntries),
                                    });
                        }
                    }

                    result[attributeName] = new AttributeValue { L = elements };
                }
                else if (ownedEntries.TryGetValue(navValue, out var ownedEntry))
                {
                    result[attributeName] = new AttributeValue
                    {
                        M = source.BuildItemFromOwnedEntry(ownedEntry, ownedEntries),
                    };
                }
            }

            return result;
        }
    }
}
