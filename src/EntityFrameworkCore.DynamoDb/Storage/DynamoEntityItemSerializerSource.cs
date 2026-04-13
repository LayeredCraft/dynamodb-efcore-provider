using System.Collections;
using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API usage

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Builds and caches typed write plans per <see cref="IEntityType"/> for SaveChanges.
/// Each plan holds one <see cref="Func{IUpdateEntry,AttributeValue}"/> per property,
/// produced at plan-build time by dispatching on the property's provider CLR type via
/// <see cref="DispatchWireType{TFactory}"/>. Write-time execution is just delegate invocation — no
/// reflection, no expression compilation, and no boxing on the hot path for well-known types.
/// </summary>
public sealed class DynamoEntityItemSerializerSource
{
    private readonly ConcurrentDictionary<IEntityType, EntityWritePlan> _cache = new();

    // Separate cache for original-value (WHERE clause) serializers, keyed by property.
    // These read the original-or-current value via IUpdateEntry.GetOriginalValue<T> /
    // GetCurrentValue<T> rather than GetCurrentValue<T> used by the INSERT/UPDATE SET path.
    private readonly ConcurrentDictionary<IProperty, Func<IUpdateEntry, AttributeValue>>
        _originalValueCache = new();

    /// <summary>
    /// Returns the fully assembled DynamoDB item dictionary for a root <see cref="IUpdateEntry"/>.
    /// Owned sub-entries are resolved on-demand via the EF state manager, scoped to what is
    /// reachable from <paramref name="rootEntry"/> — no global owned-entries dictionary is needed.
    /// </summary>
    public Dictionary<string, AttributeValue> BuildItem(IUpdateEntry rootEntry)
        => GetOrBuildPlan(rootEntry.EntityType).Serialize(rootEntry, this);

    /// <summary>
    ///     Returns the fully assembled DynamoDB item dictionary for an owned sub-entry. Exposed as
    ///     <c>internal</c> so the update path in <see cref="DynamoDatabaseWrapper" /> can serialize
    ///     OwnsOne and OwnsMany sub-documents for attribute-level replacement.
    /// </summary>
    internal Dictionary<string, AttributeValue> BuildItemFromOwnedEntry(IUpdateEntry entry)
        => GetOrBuildPlan(entry.EntityType).Serialize(entry, this);

    /// <summary>
    ///     Serializes the current value of <paramref name="property" /> on <paramref name="entry" />
    ///     to an <see cref="AttributeValue" /> using the pre-compiled per-type delegate. Used by the
    ///     update path to serialize scalar and collection-typed properties for UPDATE SET clauses.
    /// </summary>
    internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
        => GetOrBuildPlan(entry.EntityType).SerializeProperty(entry, property);

    /// <summary>
    ///     Returns a cached delegate that reads the original (or current, if no original is tracked)
    ///     value of <paramref name="property" /> from an <see cref="IUpdateEntry" /> and serializes it to
    ///     an <see cref="AttributeValue" /> without boxing.
    /// </summary>
    /// <remarks>
    ///     Used for WHERE clause key and concurrency token parameters in UPDATE and DELETE
    ///     statements. Original values are required so the correct DynamoDB item is targeted even if the
    ///     in-memory value was touched before the write was requested. The delegate is compiled once per
    ///     property and cached.
    /// </remarks>
    internal Func<IUpdateEntry, AttributeValue>
        GetOrBuildOriginalValueSerializer(IProperty property)
        => _originalValueCache.GetOrAdd(property, BuildOriginalValueSerializer);

    /// <summary>
    ///     Builds the original-value serializer delegate for <paramref name="property" />. Mirrors
    ///     <see cref="BuildScalarSerializer" /> but uses <see cref="OriginalValueScalarFactory" /> /
    ///     <see cref="OriginalValueConvertedScalarFactory" /> so the generated delegate calls
    ///     <c>GetOriginalValue&lt;T&gt;</c> / <c>GetCurrentValue&lt;T&gt;</c> rather than
    ///     <c>GetCurrentValue&lt;T&gt;</c>.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BuildOriginalValueSerializer(
        IProperty property)
    {
        var clrType = property.ClrType;
        var converter = property.GetTypeMapping().Converter;

        if (converter == null)
            return DispatchWireType(property, clrType, default(OriginalValueScalarFactory));

        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchWireType(
            property,
            converter.ProviderClrType,
            new OriginalValueConvertedScalarFactory(converter));
    }

    private EntityWritePlan GetOrBuildPlan(IEntityType entityType)
        => _cache.GetOrAdd(entityType, BuildPlan);

    private static EntityWritePlan BuildPlan(IEntityType entityType)
    {
        var properties = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            .ToList();

        // Build both the ordered writer list (for INSERT serialization) and the
        // property→serializer lookup (for UPDATE per-property serialization) in one pass
        // to avoid calling CreatePropertySerializer twice per property.
        var propertyWriters = new List<PropertyWriteAction>(properties.Count);
        var propertySerializers =
            new Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>>(properties.Count);

        foreach (var p in properties)
        {
            var serializer = CreatePropertySerializer(p);
            propertyWriters.Add(new PropertyWriteAction(p.GetAttributeName(), serializer));
            propertySerializers[p] = serializer;
        }

        var ownedNavigations = entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned())
            .ToList();

        return new EntityWritePlan(propertyWriters, propertySerializers, ownedNavigations);
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
        var propertyConverter = typeMapping.Converter;

        // Property-level converters define the store shape for the entire property, even when the
        // model CLR type itself looks like a collection.
        if (propertyConverter is not null)
            return BuildScalarSerializer(property, clrType, propertyConverter);

        // byte[] is a reference type that looks like a collection but maps to a single Binary
        // attribute.
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return BuildScalarSerializer(property, clrType, null);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(clrType, out var valueType, out _))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchWireType(property, valueType, default(DirectDictionaryFactory));
            EnsureSupportedValueProviderType(property, conv.ProviderClrType);
            return DispatchWireType(
                property,
                conv.ProviderClrType,
                new ConvertedDictionaryFactory(conv, valueType));
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchWireType(property, setElementType, default(DirectSetFactory));
            EnsureSupportedSetProviderType(property, conv.ProviderClrType);
            return DispatchWireType(
                property,
                conv.ProviderClrType,
                new ConvertedSetFactory(conv, setElementType));
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchWireType(property, listElementType, default(DirectListFactory));
            EnsureSupportedValueProviderType(property, conv.ProviderClrType);
            return DispatchWireType(
                property,
                conv.ProviderClrType,
                new ConvertedListFactory(conv, listElementType));
        }

        return BuildScalarSerializer(property, clrType, null);
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
            return DispatchWireType(property, clrType, default(DirectScalarFactory));
        EnsureSupportedValueProviderType(property, converter.ProviderClrType);
        return DispatchWireType(
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
    /// for DynamoDB-native wire types only. The if-chain runs once at plan-build time per property;
    /// the resulting delegate has no runtime type dispatch on the write path. The
    /// <c>TFactory : struct</c> constraint lets the JIT devirtualize <c>Create&lt;T&gt;</c>
    /// regardless of whether <paramref name="factory"/> is stateless (direct) or carries a
    /// <see cref="ValueConverter"/> (converter path).
    /// </summary>
    /// <remarks>
    /// Used for the outer (provider-type) dispatch pass only. The provider type is always a
    /// DynamoDB wire type — reaching the throw here indicates a bug in the type mapping layer.
    /// For the inner (model-type) pass use <see cref="TryDispatchModelType{TFactory}"/> instead,
    /// which covers common EF Core model types and returns <c>null</c> for unknown types.
    /// </remarks>
    private static Func<IUpdateEntry, AttributeValue> DispatchWireType<TFactory>(
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
            + $"wire type '{type.ShortDisplayName()}' on the write path. This is a provider bug — "
            + "the type mapping layer should have ensured a DynamoDB-native provider type.");
    }

    /// <summary>
    ///     Attempts to dispatch on <paramref name="type" /> for the inner (model-type) pass of the
    ///     converter pipeline. Covers all DynamoDB wire types plus common EF Core built-in converter model
    ///     types (<see cref="Guid" />, <see cref="DateTime" />, etc.), handled boxing-free.
    /// </summary>
    /// <returns>
    ///     The serializer delegate, or <c>null</c> when <paramref name="type" /> is not in the known
    ///     set. Callers should fall back to <c>Boxed*Fallback</c> for custom/user-defined model types.
    /// </returns>
    private static Func<IUpdateEntry, AttributeValue>? TryDispatchModelType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, ISerializerFactory
    {
        // DynamoDB wire types — also valid as converter model types.
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

        // Common CLR model types used by EF Core's built-in converters (e.g. GuidToStringConverter,
        // DateTimeToStringConverter). These are never DynamoDB wire types but are dispatched
        // boxing-free here to avoid boxing for the most common converter scenarios. Custom/user-
        // defined model types that don't appear in this list fall through to the boxed fallback.
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

    /// <summary>
    /// Attempts to dispatch on a non-nullable struct type for the inner (model-type) pass of the
    /// nullable-wrapping converter path, where <c>property.ClrType = Nullable&lt;T&gt;</c> and the
    /// converter model type is <c>T</c>. The <c>where T : struct</c> constraint on
    /// <see cref="IStructSerializerFactory"/> allows implementations to read <c>T?</c> without
    /// boxing.
    /// </summary>
    /// <returns>
    /// The serializer delegate, or <c>null</c> for unknown struct types. Callers should fall back
    /// to the boxed path.
    /// </returns>
    private static Func<IUpdateEntry, AttributeValue>? TryDispatchStructModelType<TFactory>(
        IProperty property,
        Type type,
        TFactory factory) where TFactory : struct, IStructSerializerFactory
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

    private static AttributeValue NullAttributeValue() => new() { NULL = true };

    // ── Boxed fallbacks ─────────────────────────────────────────────────────────────
    //
    // Used when the converter model type is not in TryDispatchModelType /
    // TryDispatchStructModelType (i.e., a custom/user-defined CLR type). TProvider is still
    // statically known from the outer DispatchWireType pass, so the final AttributeValue
    // construction remains unboxed.
    //
    // Important: GetCurrentValue<object>(property) cannot be used here because EF Core's
    // implementation casts the compiled Func<IInternalEntry,TModel> delegate to
    // Func<IInternalEntry,object>, which fails for value-type model types due to generic delegate
    // invariance. The scalar fallback uses GetCurrentProviderValue() instead; collection fallbacks
    // use the InternalEntityEntry indexer (already used elsewhere in this file).
    //
    // For well-known EF Core model types (Guid, DateTime, etc.) the typed path is taken instead and
    // these methods are never called. Custom types with converters that produce unsupported
    // provider
    // types are rejected earlier by EnsureSupportedValueProviderType.

    /// <summary>
    ///     Boxed fallback for scalar properties whose converter model type is not in the dispatch
    ///     table. Uses <c>IUpdateEntry.GetCurrentProviderValue</c> which boxes the model value and applies
    ///     the converter internally — avoiding the generic delegate invariance issue with
    ///     <c>GetCurrentValue&lt;object&gt;</c>. The resulting provider value is cast to
    ///     <typeparamref name="TProvider" /> (known statically) before constructing the
    ///     <see cref="AttributeValue" />.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BoxedScalarFallback<TProvider>(
        IProperty property)
        => entry =>
        {
            // GetCurrentProviderValue boxes the model value and applies the converter, returning
            // the provider-typed value as object? — safe for any model CLR type.
            var providerValue = entry.GetCurrentProviderValue(property);
            if (providerValue is null)
                return NullAttributeValue();
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                (TProvider)providerValue);
        };

    /// <summary>
    ///     Boxed fallback for list properties whose converter element model type is not in the
    ///     dispatch table. Uses the <see cref="InternalEntityEntry" /> indexer to read the raw collection
    ///     as <c>object?</c>, then boxes each element during iteration and applies the element converter.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BoxedListFallback<TProvider>(
        IProperty property,
        ValueConverter converter)
        => entry =>
        {
            var raw = ((InternalEntityEntry)entry)[property];
            if (raw is null)
                return NullAttributeValue();
            var list = ((IEnumerable)raw)
                .Cast<object?>()
                .Select(element =>
                {
                    if (element is null)
                        return NullAttributeValue();
                    var providerValue = (TProvider)converter.ConvertToProvider(element)!;
                    return DynamoWireValueConversion
                        .ConvertProviderValueToAttributeValue(providerValue);
                })
                .ToList();
            return new AttributeValue { L = list };
        };

    /// <summary>
    ///     Boxed fallback for set properties whose converter element model type is not in the
    ///     dispatch table. Uses the <see cref="InternalEntityEntry" /> indexer to read the raw collection
    ///     as <c>object?</c>, then boxes each element during iteration and applies the element converter.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BoxedSetFallback<TProvider>(
        IProperty property,
        ValueConverter converter)
        => entry =>
        {
            var raw = ((InternalEntityEntry)entry)[property];
            if (raw is null)
                return NullAttributeValue();
            var converted = ((IEnumerable)raw)
                .Cast<object?>()
                .Select(element =>
                {
                    if (element is null)
                        throw new InvalidOperationException(
                            $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                            + "DynamoDB sets cannot contain null values.");
                    return (TProvider)converter.ConvertToProvider(element)!;
                });
            return DynamoAttributeValueCollectionHelpers.SerializeSet(converted);
        };

    /// <summary>
    ///     Boxed fallback for dictionary properties whose converter value model type is not in the
    ///     dispatch table. Uses the <see cref="InternalEntityEntry" /> indexer to read the raw dictionary
    ///     as <c>object?</c>, then boxes each value via <see cref="IDictionary" /> and applies the element
    ///     converter.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> BoxedDictionaryFallback<TProvider>(
        IProperty property,
        ValueConverter converter)
        => entry =>
        {
            var raw = ((InternalEntityEntry)entry)[property];
            if (raw is null)
                return NullAttributeValue();
            // Dictionary<string, TValue> implements IDictionary, giving non-generic key/value
            // access.
            var dict = (IDictionary)raw;
            var result = new Dictionary<string, AttributeValue>(dict.Count, StringComparer.Ordinal);
            foreach (DictionaryEntry kvp in dict)
            {
                AttributeValue attributeValue;
                if (kvp.Value is null)
                {
                    attributeValue = NullAttributeValue();
                }
                else
                {
                    var providerValue = (TProvider)converter.ConvertToProvider(kvp.Value)!;
                    attributeValue =
                        DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                            providerValue);
                }

                result[(string)kvp.Key] = attributeValue;
            }

            return new AttributeValue { M = result };
        };

    // ── Interfaces ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Unified factory interface for both direct (stateless) and converter-path (stateful)
    /// property serializers. The <c>TFactory : struct</c> constraint on
    /// <see cref="DispatchWireType{TFactory}"/> lets the JIT devirtualize <c>Create&lt;T&gt;</c> for all
    /// implementations without virtual dispatch overhead.
    /// </summary>
    private interface ISerializerFactory
    {
        Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property);
    }

    /// <summary>
    ///     Variant of <see cref="ISerializerFactory" /> whose <c>Create</c> method is constrained to
    ///     struct (value) types. Used by <see cref="TryDispatchStructModelType{TFactory}" /> for the
    ///     nullable-wrapping converter path so that implementations can write <c>T?</c> (resolved as
    ///     <c>Nullable&lt;T&gt;</c>) without boxing or a secondary helper.
    /// </summary>
    private interface IStructSerializerFactory
    {
        Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property) where T : struct;
    }

    // ── Direct (no converter) factories ────────────────────────────────────────────
    // Stateless — passed as default(TFactory). The JIT eliminates the zero-byte struct entirely.

    private readonly struct DirectScalarFactory : ISerializerFactory
    {
        /// <summary>
        ///     Reads the typed property value and converts it directly to an
        ///     <see cref="AttributeValue" />.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                entry.GetCurrentValue<T>(property));
    }

    // ── Original-value (WHERE clause) factories ────────────────────────────────────
    //
    // Mirror DirectScalarFactory / ConvertedScalarFactory but read the original-or-current
    // value via IUpdateEntry.GetOriginalValue<T> / GetCurrentValue<T> so that WHERE clause
    // key and concurrency token parameters target the correct DynamoDB item even when the
    // in-memory value has been mutated since the snapshot was taken.

    /// <summary>
    ///     No-converter original-value factory. Reads the original (or current) value via the typed
    ///     <c>GetOriginalValue&lt;T&gt;</c> / <c>GetCurrentValue&lt;T&gt;</c> accessors — both are backed
    ///     by pre-compiled <c>Func&lt;IInternalEntry, T&gt;</c> delegates and do not box value types.
    /// </summary>
    private readonly struct OriginalValueScalarFactory : ISerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that reads the original-or-current property value without boxing and
        ///     converts it to an <see cref="AttributeValue" />.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                entry.CanHaveOriginalValue(property)
                    ? entry.GetOriginalValue<T>(property)
                    : entry.GetCurrentValue<T>(property));
    }

    /// <summary>
    ///     Converter-path original-value outer factory for scalar properties. On
    ///     <c>Create&lt;TProvider&gt;</c> dispatches on the converter model type exactly as
    ///     <see cref="ConvertedScalarFactory" /> does, but uses
    ///     <see cref="OriginalValueScalarModelBinder{TProvider}" /> /
    ///     <see cref="OriginalValueScalarNullableBinder{TProvider}" /> so the inner delegate reads the
    ///     original value rather than the current value.
    /// </summary>
    private readonly struct OriginalValueConvertedScalarFactory(ValueConverter converter)
        : ISerializerFactory
    {
        /// <summary>
        ///     Dispatches on the converter model type to bind a typed original-value scalar delegate.
        ///     Handles the direct-match and nullable-wrapping cases without boxing. Falls back to a boxed path
        ///     for custom model types not in the dispatch table.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var converterModelType = converter.ModelClrType;
            var propClrType = property.ClrType;

            if (propClrType == converterModelType)
                return TryDispatchModelType(
                        property,
                        converterModelType,
                        new OriginalValueScalarModelBinder<TProvider>(converter))
                    ?? BoxedOriginalValueScalarFallback<TProvider>(property, converter);

            // Nullable wrapping: property is Nullable<TUnderlying>, converter model is TUnderlying.
            var underlying = Nullable.GetUnderlyingType(propClrType);
            if (underlying == converterModelType)
                return TryDispatchStructModelType(
                        property,
                        converterModelType,
                        new OriginalValueScalarNullableBinder<TProvider>(converter))
                    ?? BoxedOriginalValueScalarFallback<TProvider>(property, converter);

            throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                + $"property CLR type '{propClrType.ShortDisplayName()}' cannot be bound to "
                + $"converter model type '{converterModelType.ShortDisplayName()}' on the "
                + "original-value write path. The converter model type must match the property "
                + "CLR type or be its non-nullable underlying type.");
        }
    }

    /// <summary>
    ///     Inner binder for the direct-match converter + original-value path. Reads the original (or
    ///     current) model value and converts it to the provider type without boxing.
    /// </summary>
    private readonly struct OriginalValueScalarModelBinder<TProvider>(ValueConverter converter)
        : ISerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that reads the original model value, converts it via
        ///     <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" />, and serializes to an
        ///     <see cref="AttributeValue" /> without boxing.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TModel>(IProperty property)
        {
            var typed = (ValueConverter<TModel, TProvider>)converter;
            return entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                typed.ConvertToProviderTyped(
                    entry.CanHaveOriginalValue(property)
                        ? entry.GetOriginalValue<TModel>(property)
                        : entry.GetCurrentValue<TModel>(property)));
        }
    }

    /// <summary>
    ///     Inner binder for the nullable-wrapping converter + original-value path. Property CLR type
    ///     is <c>Nullable&lt;TUnderlying&gt;</c>, converter model type is <c>TUnderlying</c>. Reads the
    ///     original nullable value and converts only when non-null.
    /// </summary>
    private readonly struct OriginalValueScalarNullableBinder<TProvider>(ValueConverter converter)
        : IStructSerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that reads the original nullable value and, when present, converts it
        ///     to the provider type without boxing.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TUnderlying>(IProperty property)
            where TUnderlying : struct
        {
            var typed = (ValueConverter<TUnderlying, TProvider>)converter;
            return entry =>
            {
                var value = entry.CanHaveOriginalValue(property)
                    ? entry.GetOriginalValue<TUnderlying?>(property)
                    : entry.GetCurrentValue<TUnderlying?>(property);
                return value.HasValue
                    ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                        typed.ConvertToProviderTyped(value.Value))
                    : NullAttributeValue();
            };
        }
    }

    /// <summary>
    ///     Boxed fallback for the original-value converter path when the model CLR type is not in the
    ///     <see cref="TryDispatchModelType{TFactory}" /> dispatch table (i.e. a custom type).
    ///     <typeparamref name="TProvider" /> is still statically known, so the final
    ///     <see cref="AttributeValue" /> construction stays unboxed.
    /// </summary>
    /// <remarks>
    ///     Only reached for custom model types; all standard EF Core model types (Guid, DateTime,
    ///     etc.) are handled by the typed dispatch path above.
    /// </remarks>
    private static Func<IUpdateEntry, AttributeValue> BoxedOriginalValueScalarFallback<TProvider>(
        IProperty property,
        ValueConverter converter)
        => entry =>
        {
            // GetOriginalValue(property) returns object? — boxes for value types, but only
            // reachable here for custom model types not in the dispatch table.
            var originalModelValue = entry.CanHaveOriginalValue(property)
                ? entry.GetOriginalValue(property)
                : entry.GetCurrentValue(property);
            if (originalModelValue is null)
                return NullAttributeValue();
            var providerValue = converter.ConvertToProvider(originalModelValue);
            if (providerValue is null)
                return NullAttributeValue();
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                (TProvider)providerValue);
        };

    private readonly struct DirectListFactory : ISerializerFactory
    {
        /// <summary>Reads the typed enumerable and serializes it as a DynamoDB list (L).</summary>
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<T>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeList(value);
            };
    }

    private readonly struct DirectSetFactory : ISerializerFactory
    {
        /// <summary>Reads the typed enumerable and serializes it as a DynamoDB set (SS/NS/BS).</summary>
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<T>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeSet(value);
            };
    }

    private readonly struct DirectDictionaryFactory : ISerializerFactory
    {
        /// <summary>Reads the typed enumerable of key-value pairs and serializes it as a DynamoDB map (M).</summary>
        public Func<IUpdateEntry, AttributeValue> Create<T>(IProperty property)
            => entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<KeyValuePair<string, T>>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeDictionary(value);
            };
    }

    // ── Outer converter-path factories ─────────────────────────────────────────────
    //
    // DispatchWireType gives us TProvider. Inside Create<TProvider>, a second dispatch on the
    // converter model type (or collection element type) produces the final delegate via a typed
    // binder struct (for well-known types) or a boxed fallback (for custom model types).
    // The element type is stored at construction time to avoid re-inspecting the property CLR type.

    /// <summary>
    /// Converter-path outer factory for scalar properties. On <c>Create&lt;TProvider&gt;</c>,
    /// dispatches on the converter model type to select a typed inner binder, which accesses
    /// <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped"/> — the pre-compiled
    /// typed delegate — without boxing.
    /// </summary>
    private readonly struct ConvertedScalarFactory(ValueConverter converter) : ISerializerFactory
    {
        /// <summary>
        ///     Dispatches on the converter model type to bind a typed scalar write delegate. Handles
        ///     direct match (property CLR type == converter model type) and nullable wrapping (property CLR
        ///     type is <c>Nullable&lt;T&gt;</c>, converter model type is <c>T</c>).
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var converterModelType = converter.ModelClrType;
            var propClrType = property.ClrType;

            if (propClrType == converterModelType)
                return TryDispatchModelType(
                        property,
                        converterModelType,
                        new ScalarModelBinder<TProvider>(converter))
                    ?? BoxedScalarFallback<TProvider>(property);

            // Nullable wrapping: property is Nullable<TUnderlying>, converter model is TUnderlying.
            var underlying = Nullable.GetUnderlyingType(propClrType);
            if (underlying == converterModelType)
                return TryDispatchStructModelType(
                        property,
                        converterModelType,
                        new ScalarNullableBinder<TProvider>(converter))
                    ?? BoxedScalarFallback<TProvider>(property);

            throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                + $"property CLR type '{propClrType.ShortDisplayName()}' cannot be bound to "
                + $"converter model type '{converterModelType.ShortDisplayName()}' on the write path. "
                + "The converter model type must match the property CLR type or be its non-nullable underlying type.");
        }
    }

    /// <summary>
    /// Converter-path outer factory for list properties. Stores the list element type at
    /// construction time to avoid re-inspecting the property CLR type inside
    /// <c>Create&lt;TProvider&gt;</c>.
    /// </summary>
    private readonly struct ConvertedListFactory(ValueConverter converter, Type elementType)
        : ISerializerFactory
    {
        /// <summary>Dispatches on the list element type to bind a typed list write delegate.</summary>
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var converterModelType = converter.ModelClrType;

            if (elementType == converterModelType)
                return TryDispatchModelType(
                        property,
                        converterModelType,
                        new ListModelBinder<TProvider>(converter))
                    ?? BoxedListFallback<TProvider>(property, converter);

            var underlying = Nullable.GetUnderlyingType(elementType);
            if (underlying == converterModelType)
                return TryDispatchStructModelType(
                        property,
                        converterModelType,
                        new ListNullableBinder<TProvider>(converter))
                    ?? BoxedListFallback<TProvider>(property, converter);

            throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                + $"list element type '{elementType.ShortDisplayName()}' cannot be bound to "
                + $"converter model type '{converterModelType.ShortDisplayName()}' on the write path.");
        }
    }

    /// <summary>
    ///     Converter-path outer factory for set properties. Stores the set element type at
    ///     construction time.
    /// </summary>
    private readonly struct ConvertedSetFactory(ValueConverter converter, Type elementType)
        : ISerializerFactory
    {
        /// <summary>
        /// Dispatches on the set element type to bind a typed set write delegate.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var converterModelType = converter.ModelClrType;

            if (elementType == converterModelType)
                return TryDispatchModelType(
                        property,
                        converterModelType,
                        new SetModelBinder<TProvider>(converter))
                    ?? BoxedSetFallback<TProvider>(property, converter);

            var underlying = Nullable.GetUnderlyingType(elementType);
            if (underlying == converterModelType)
                return TryDispatchStructModelType(
                        property,
                        converterModelType,
                        new SetNullableBinder<TProvider>(converter))
                    ?? BoxedSetFallback<TProvider>(property, converter);

            throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                + $"set element type '{elementType.ShortDisplayName()}' cannot be bound to "
                + $"converter model type '{converterModelType.ShortDisplayName()}' on the write path.");
        }
    }

    /// <summary>
    ///     Converter-path outer factory for dictionary properties. Stores the dictionary value type
    ///     at construction time.
    /// </summary>
    private readonly struct ConvertedDictionaryFactory(ValueConverter converter, Type valueType)
        : ISerializerFactory
    {
        /// <summary>Dispatches on the dictionary value type to bind a typed dictionary write delegate.</summary>
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var converterModelType = converter.ModelClrType;

            if (valueType == converterModelType)
                return TryDispatchModelType(
                        property,
                        converterModelType,
                        new DictionaryModelBinder<TProvider>(converter))
                    ?? BoxedDictionaryFallback<TProvider>(property, converter);

            var underlying = Nullable.GetUnderlyingType(valueType);
            if (underlying == converterModelType)
                return TryDispatchStructModelType(
                        property,
                        converterModelType,
                        new DictionaryNullableBinder<TProvider>(converter))
                    ?? BoxedDictionaryFallback<TProvider>(property, converter);

            throw new NotSupportedException(
                $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
                + $"dictionary value type '{valueType.ShortDisplayName()}' cannot be bound to "
                + $"converter model type '{converterModelType.ShortDisplayName()}' on the write path.");
        }
    }

    // ── Model-type binders ─────────────────────────────────────────────────────────
    //
    // Innermost factories — produced after double dispatch (provider type + model/element type).
    // Both TProvider (from the outer DispatchWireType) and TModel/TElement (from the inner
    // dispatch)
    // are resolved as type parameters, so ValueConverter<TModel, TProvider>.ConvertToProviderTyped
    // is accessed with full type safety and no boxing.
    //
    // ConvertToProviderTyped is lazily compiled by EF Core (once, cached). When an EF Core compiled
    // model is used, this may already be pre-compiled at publish time. Either way, no complex
    // expression trees are built by this provider on the write path.

    /// <summary>
    ///     Scalar binder for the direct-match path (<c>property.ClrType == converter.ModelClrType</c>
    ///     ). Casts the base converter to the typed <see cref="ValueConverter{TModel,TProvider}" /> and
    ///     uses <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" /> to convert without
    ///     boxing.
    /// </summary>
    private readonly struct ScalarModelBinder<TProvider>(ValueConverter converter)
        : ISerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that converts a scalar model value to an <see cref="AttributeValue" />
        ///     via the typed converter.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TModel>(IProperty property)
        {
            var typed = (ValueConverter<TModel, TProvider>)converter;
            return entry => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                typed.ConvertToProviderTyped(entry.GetCurrentValue<TModel>(property)));
        }
    }

    /// <summary>
    ///     Scalar binder for the nullable-wrapping path (<c>property.ClrType = Nullable&lt;T&gt;</c>,
    ///     <c>converter.ModelClrType = T</c>). The <c>where T : struct</c> constraint from
    ///     <see cref="IStructSerializerFactory" /> enables reading <c>T?</c> without boxing.
    /// </summary>
    private readonly struct ScalarNullableBinder<TProvider>(ValueConverter converter)
        : IStructSerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that reads a nullable property value and applies the converter to the
        ///     non-null underlying value, returning <c>{ NULL = true }</c> when the value is absent.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TUnderlying>(IProperty property)
            where TUnderlying : struct
        {
            var typed = (ValueConverter<TUnderlying, TProvider>)converter;
            return entry =>
            {
                var value = entry.GetCurrentValue<TUnderlying?>(property);
                return value.HasValue
                    ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                        typed.ConvertToProviderTyped(value.Value))
                    : NullAttributeValue();
            };
        }
    }

    /// <summary>
    ///     List binder for the direct-match path (element type == converter model type). Passes
    ///     <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" /> directly to the typed
    ///     collection helper, which iterates without boxing.
    /// </summary>
    private readonly struct ListModelBinder<TProvider>(ValueConverter converter)
        : ISerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that serializes an enumerable of converted elements to a DynamoDB list
        ///     (L).
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TElement>(IProperty property)
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TElement>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeList(
                        value,
                        typed.ConvertToProviderTyped);
            };
        }
    }

    /// <summary>
    /// List binder for the nullable-wrapping path (element type = <c>Nullable&lt;T&gt;</c>,
    /// converter model type = <c>T</c>). Null elements are serialized explicitly as
    /// <c>{ NULL = true }</c> list entries.
    /// </summary>
    private readonly struct ListNullableBinder<TProvider>(ValueConverter converter)
        : IStructSerializerFactory
    {
        /// <summary>
        /// Returns a delegate that serializes an enumerable of nullable elements to a DynamoDB list (L),
        /// converting each non-null element and serializing null elements as <c>{ NULL = true }</c>.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TUnderlying>(IProperty property)
            where TUnderlying : struct
        {
            var typed = (ValueConverter<TUnderlying, TProvider>)converter;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TUnderlying?>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeList(
                        value,
                        item => item.HasValue
                            ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                                typed.ConvertToProviderTyped(item.Value))
                            : NullAttributeValue());
            };
        }
    }

    /// <summary>Set binder for the direct-match path (element type == converter model type).</summary>
    private readonly struct SetModelBinder<TProvider>(ValueConverter converter) : ISerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that serializes an enumerable of converted elements to a DynamoDB set
        ///     (SS/NS/BS).
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TElement>(IProperty property)
        {
            var typed = (ValueConverter<TElement, TProvider>)converter;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TElement>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeSet(
                        value,
                        typed.ConvertToProviderTyped);
            };
        }
    }

    /// <summary>
    ///     Set binder for the nullable-wrapping path (element type = <c>Nullable&lt;T&gt;</c>,
    ///     converter model type = <c>T</c>). Null elements are rejected because DynamoDB sets
    ///     cannot contain null values.
    /// </summary>
    private readonly struct SetNullableBinder<TProvider>(ValueConverter converter)
        : IStructSerializerFactory
    {
        /// <summary>
        ///     Returns a delegate that serializes an enumerable of nullable elements to a DynamoDB set
        ///     (SS/NS/BS), rejecting null elements.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TUnderlying>(IProperty property)
            where TUnderlying : struct
        {
            var typed = (ValueConverter<TUnderlying, TProvider>)converter;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TUnderlying?>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeSet(
                        value,
                        item => item.HasValue
                            ? typed.ConvertToProviderTyped(item.Value)
                            : throw new InvalidOperationException(
                                "DynamoDB sets cannot contain null elements."));
            };
        }
    }

    /// <summary>
    ///     Dictionary binder for the direct-match path (dictionary value type == converter model
    ///     type).
    /// </summary>
    private readonly struct DictionaryModelBinder<TProvider>(ValueConverter converter)
        : ISerializerFactory
    {
        /// <summary>Returns a delegate that serializes a dictionary of converted values to a DynamoDB map (M).</summary>
        public Func<IUpdateEntry, AttributeValue> Create<TValue>(IProperty property)
        {
            var typed = (ValueConverter<TValue, TProvider>)converter;
            return entry =>
            {
                var value =
                    entry.GetCurrentValue<IEnumerable<KeyValuePair<string, TValue>>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                        value,
                        typed.ConvertToProviderTyped);
            };
        }
    }

    /// <summary>
    ///     Dictionary binder for the nullable-wrapping path (value type = <c>Nullable&lt;T&gt;</c>,
    ///     converter model type = <c>T</c>). Null values are serialized explicitly as
    ///     <c>{ NULL = true }</c> map entries.
    /// </summary>
    private readonly struct DictionaryNullableBinder<TProvider>(ValueConverter converter)
        : IStructSerializerFactory
    {
        /// <summary>
        /// Returns a delegate that serializes a dictionary of nullable values to a DynamoDB map (M),
        /// converting each non-null value and serializing null values as <c>{ NULL = true }</c>.
        /// </summary>
        public Func<IUpdateEntry, AttributeValue> Create<TUnderlying>(IProperty property)
            where TUnderlying : struct
        {
            var typed = (ValueConverter<TUnderlying, TProvider>)converter;
            return entry =>
            {
                var value =
                    entry.GetCurrentValue<IEnumerable<KeyValuePair<string, TUnderlying?>>>(
                        property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                        value,
                        item => item.HasValue
                            ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                                typed.ConvertToProviderTyped(item.Value))
                            : NullAttributeValue());
            };
        }
    }

    private readonly record struct PropertyWriteAction(
        string AttributeName,
        Func<IUpdateEntry, AttributeValue> Serialize);

    private sealed class EntityWritePlan(
        List<PropertyWriteAction> propertyWriters,
        Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>> propertySerializers,
        List<INavigation> ownedNavigations)
    {
        /// <summary>
        ///     Serializes the current value of <paramref name="property" /> for <paramref name="entry" />
        ///     using the pre-compiled delegate built at plan-construction time. Used by the UPDATE path to
        ///     serialize individual properties without rebuilding a full item dictionary.
        /// </summary>
        internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
        {
            if (!propertySerializers.TryGetValue(property, out var serializer))
                throw new InvalidOperationException(
                    $"No serializer was built for property "
                    + $"'{property.DeclaringType?.DisplayName()}.{property.Name}'. "
                    + "Shadow key properties are not serialized.");

            return serializer(entry);
        }

        /// <summary>
        ///     Serializes all scalar properties of <paramref name="entry" /> into a DynamoDB item
        ///     dictionary, then resolves and serializes any owned navigation sub-entries.
        /// </summary>
        public Dictionary<string, AttributeValue> Serialize(
            IUpdateEntry entry,
            DynamoEntityItemSerializerSource source)
        {
            var result = new Dictionary<string, AttributeValue>(
                propertyWriters.Count + ownedNavigations.Count,
                StringComparer.Ordinal);

            foreach (var writer in propertyWriters)
                result[writer.AttributeName] = writer.Serialize(entry);

            if (ownedNavigations.Count == 0)
                return result;

            // Resolve owned entries on-demand via the state manager, scoped to navigations
            // reachable from this entry. This avoids passing a global owned-entries dictionary
            // that spans all root entities being saved in a single SaveChanges call.
            var stateManager = ((InternalEntityEntry)entry).StateManager;

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
                            if (element is null)
                                continue;
                            var ownedEntry =
                                stateManager.TryGetEntry(element, nav.TargetEntityType);
                            if (ownedEntry is not null)
                                elements.Add(
                                    new AttributeValue
                                    {
                                        M = source.BuildItemFromOwnedEntry(ownedEntry),
                                    });
                        }
                    }

                    result[attributeName] = new AttributeValue { L = elements };
                }
                else
                {
                    var ownedEntry = stateManager.TryGetEntry(navValue, nav.TargetEntityType);
                    if (ownedEntry is not null)
                        result[attributeName] = new AttributeValue
                        {
                            M = source.BuildItemFromOwnedEntry(ownedEntry),
                        };
                }
            }

            return result;
        }
    }
}
