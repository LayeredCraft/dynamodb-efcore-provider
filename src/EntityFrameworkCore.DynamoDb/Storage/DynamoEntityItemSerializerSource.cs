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
/// Each plan holds one pre-compiled <see cref="Func{IUpdateEntry,AttributeValue}"/> per property,
/// produced at plan-build time by dispatching on the property's provider CLR type via
/// <see cref="DispatchType{TFactory}"/>. Write-time execution is just delegate invocation — no
/// reflection, no boxing beyond what EF Core's ValueConverter API requires.
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
    private Dictionary<string, AttributeValue> BuildItemFromOwnedEntry(
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
            .Select(static p
                => new PropertyWriteAction(p.GetAttributeName(), CreatePropertySerializer(p)))
            .ToList();

        var ownedNavigations = entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned())
            .ToList();

        return new EntityWritePlan(propertyWriters, ownedNavigations);
    }

    /// <summary>
    /// Selects and builds the typed write delegate for a single property. Detects the collection
    /// shape (dictionary, set, list) or falls back to scalar, then branches on whether an element-
    /// or property-level converter is present to pick the right factory.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CreatePropertySerializer(IProperty property)
    {
        var clrType = property.ClrType;
        var typeMapping = property.GetTypeMapping();

        // byte[] is a reference type that looks like a collection but maps to a single Binary
        // attribute.
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return BuildScalarSerializer(property, clrType, typeMapping.Converter);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(clrType, out var valueType, out _))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchType(property, valueType, default(DirectDictionaryFactory));
            EnsureSupportedValueProviderType(property, conv.ProviderClrType);
            return DispatchType(
                property,
                conv.ProviderClrType,
                new ConvertedDictionaryFactory(conv));
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchType(property, setElementType, default(DirectSetFactory));
            EnsureSupportedSetProviderType(property, conv.ProviderClrType);
            return DispatchType(property, conv.ProviderClrType, new ConvertedSetFactory(conv));
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchType(property, listElementType, default(DirectListFactory));
            EnsureSupportedValueProviderType(property, conv.ProviderClrType);
            return DispatchType(property, conv.ProviderClrType, new ConvertedListFactory(conv));
        }

        return BuildScalarSerializer(property, clrType, typeMapping.Converter);
    }

    /// <summary>
    /// Builds a scalar write delegate, dispatching on the provider CLR type (after applying the
    /// converter if present).
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BuildScalarSerializer(
        IProperty property,
        Type clrType,
        ValueConverter? converter)
    {
        if (converter == null)
            return DispatchType(property, clrType, default(DirectScalarFactory));
        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchType(
            property,
            converter.ProviderClrType,
            new ConvertedScalarFactory(converter));
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

    /// <summary>
    /// Dispatches on <paramref name="type"/> to call <c>factory.Create&lt;T&gt;(property)</c>
    /// with the matching type argument. The if-chain runs once at plan-build time per property;
    /// the resulting delegate has no runtime type dispatch on the write path. The
    /// <c>TFactory : struct</c> constraint lets the JIT devirtualize <c>Create&lt;T&gt;</c>
    /// regardless of whether <paramref name="factory"/> is stateless (direct) or carries a
    /// <see cref="ValueConverter"/> (converter path).
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> DispatchType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, ISerializerFactory
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

    private static AttributeValue NullAttributeValue() => new() { NULL = true };

    /// <summary>
    ///     Unified factory interface for both direct (stateless) and converter-path (stateful)
    ///     property serializers. The <c>TFactory : struct</c> constraint on
    ///     <see cref="DispatchType{TFactory}" /> lets the JIT devirtualize <c>Create&lt;T&gt;</c> for all
    ///     implementations without virtual dispatch overhead.
    /// </summary>
    private interface ISerializerFactory
    {
        Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property);
    }

    // ── Direct (no converter) factories ────────────────────────────────────────
    // Stateless — passed as default(TFactory). The JIT eliminates the zero-byte struct entirely.

    private readonly struct DirectScalarFactory : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                entry.GetCurrentValue<T>(property));
    }

    private readonly struct DirectListFactory : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<T>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeList<IEnumerable<T>, T>(value);
            };
    }

    private readonly struct DirectSetFactory : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<T>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeSet<IEnumerable<T>, T>(value);
            };
    }

    private readonly struct DirectDictionaryFactory : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<KeyValuePair<string, T>>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers
                        .SerializeDictionary<IEnumerable<KeyValuePair<string, T>>, T>(value);
            };
    }

    // ── Converter-path factories ────────────────────────────────────────────────
    //
    // Provider type TProvider is resolved at plan-build time via DispatchType so that
    // ConvertProviderValueToAttributeValue<TProvider> is JIT-specialized per type. The converter's
    // ConvertToProvider(object?) is the unavoidable boxing boundary — EF Core's ValueConverter
    // API is object-typed at the model→provider boundary.

    /// <summary>
    /// Converter-path factory for scalar properties. Dispatches on the converter's provider type
    /// at plan-build time.
    /// </summary>
    private readonly struct ConvertedScalarFactory : ISerializerFactory
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
                    return NullAttributeValue();
                return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    (TProvider)providerValue);
            };
        }
    }

    /// <summary>
    /// Converter-path factory for list properties. Passes the converter's
    /// <see cref="ValueConverter.ConvertToProvider"/> delegate directly to the typed list helper.
    /// </summary>
    private readonly struct ConvertedListFactory : ISerializerFactory
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
                    return NullAttributeValue();
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
    /// Converter-path factory for set properties. Accumulates directly into SS/NS/BS without
    /// creating intermediate <see cref="AttributeValue"/> objects per element.
    /// </summary>
    private readonly struct ConvertedSetFactory : ISerializerFactory
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
                    return NullAttributeValue();
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
    /// Converter-path factory for dictionary properties. Passes the converter's
    /// <see cref="ValueConverter.ConvertToProvider"/> delegate directly to the typed map helper.
    /// </summary>
    private readonly struct ConvertedDictionaryFactory : ISerializerFactory
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
                    return NullAttributeValue();
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
