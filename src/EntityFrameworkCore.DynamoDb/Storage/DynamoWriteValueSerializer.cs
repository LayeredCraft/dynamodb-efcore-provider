using System.Collections;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
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

    /// <summary>
    ///     Builds the original-value serializer delegate for <paramref name="property" />. Mirrors
    ///     <see cref="BuildScalarSerializer" /> but uses <see cref="OriginalValueScalarFactory" /> /
    ///     <see cref="OriginalValueConvertedScalarFactory" /> so the generated delegate calls
    ///     <c>GetOriginalValue&lt;T&gt;</c> / <c>GetCurrentValue&lt;T&gt;</c> rather than
    ///     <c>GetCurrentValue&lt;T&gt;</c>.
    /// </summary>
    internal static Func<IUpdateEntry, AttributeValue> CreateOriginalValueSerializer(
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

    /// <summary>
    /// Selects and builds the typed write delegate for a single property. Detects the collection
    /// shape (dictionary, set, list) or falls back to scalar, then branches on whether an element-
    /// or property-level converter is present to pick the right factory.
    /// </summary>
    internal static Func<IUpdateEntry, AttributeValue> CreateCurrentValueSerializer(
        IProperty property)
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

    // ── Direct (no converter) factories ────────────────────────────────────────────
    // Stateless — passed as default(TFactory). The JIT eliminates the zero-byte struct entirely.

    private readonly struct DirectScalarFactory : IDynamoWriteValueSerializerFactory
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
    private readonly struct OriginalValueScalarFactory : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteStructValueSerializerFactory
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

    private readonly struct DirectListFactory : IDynamoWriteValueSerializerFactory
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

    private readonly struct DirectSetFactory : IDynamoWriteValueSerializerFactory
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

    private readonly struct DirectDictionaryFactory : IDynamoWriteValueSerializerFactory
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
    private readonly struct ConvertedScalarFactory(ValueConverter converter)
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
    ///     struct serializer factory enables reading <c>T?</c> without boxing.
    /// </summary>
    private readonly struct ScalarNullableBinder<TProvider>(ValueConverter converter)
        : IDynamoWriteStructValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteStructValueSerializerFactory
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
    private readonly struct SetModelBinder<TProvider>(ValueConverter converter)
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteStructValueSerializerFactory
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
        : IDynamoWriteValueSerializerFactory
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
        : IDynamoWriteStructValueSerializerFactory
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

    /// <summary>Creates a complex CLR instance serializer for a scalar property.</summary>
    internal static Func<object, AttributeValue> CreateComplexValueSerializer(IProperty property)
    {
        var serializeValue = CreateObjectValueSerializer(property);
        return instance => serializeValue(property.GetGetter().GetClrValue(instance));
    }

    /// <summary>Creates an object-edge serializer for an already-read scalar property value.</summary>
    internal static Func<object?, AttributeValue> CreateObjectValueSerializer(IProperty property)
    {
        var clrType = property.ClrType;
        var typeMapping = property.GetTypeMapping();
        var propertyConverter = typeMapping.Converter;

        if (propertyConverter is not null)
            return value => value is null
                ? NullAttributeValue()
                : ConvertProviderShapeToAttributeValue(propertyConverter.ConvertToProvider(value));

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return value => value is null
                ? NullAttributeValue()
                : DynamoWireValueConversion.ConvertProviderValueToAttributeValue((byte[])value);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out _))
        {
            var valueConverter = typeMapping.ElementTypeMapping?.Converter;
            return value =>
            {
                if (value is null)
                    return NullAttributeValue();
                return valueConverter is null
                    ? SerializeDirectScalarDictionary(value, dictionaryValueType)
                    : SerializeConvertedScalarDictionary(value, valueConverter);
            };
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return value =>
            {
                if (value is null)
                    return NullAttributeValue();
                return elementConverter is null
                    ? SerializeDirectScalarSet(value, setElementType)
                    : SerializeConvertedScalarSet(value, elementConverter);
            };
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return value =>
            {
                if (value is null)
                    return NullAttributeValue();
                return elementConverter is null
                    ? SerializeDirectScalarList(value, listElementType)
                    : SerializeConvertedScalarList(value, elementConverter);
            };
        }

        return value => value is null
            ? NullAttributeValue()
            : DynamoWireValueConversion.ConvertProviderValueToAttributeValue(value);
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

    /// <summary>
    ///     Serializes a scalar dictionary with no element converter. Known DynamoDB wire values use
    ///     closed generic collection helpers; other direct value types intentionally fall through to the
    ///     boxed path so unsupported shapes fail with explicit wire-conversion errors.
    /// </summary>
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

    /// <summary>
    ///     Serializes a scalar set with no element converter. The boxed fallback is intentional for supported
    ///     numeric element types outside the closed-helper fast path; unsupported direct set element types are
    ///     rejected by type mapping before serializer execution.
    /// </summary>
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

    /// <summary>
    ///     Serializes a scalar list with no element converter. Known DynamoDB wire values use closed
    ///     generic collection helpers; unsupported direct values fall through to boxed wire conversion so
    ///     the failure message names the unsupported CLR value type.
    /// </summary>
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
