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
/// Each plan holds one pre-compiled <see cref="Func{IUpdateEntry,AttributeValue}"/> per property,
/// produced at plan-build time by dispatching on the property's provider CLR type via
/// <see cref="DispatchType{TFactory}"/>. Write-time execution is just delegate invocation — no
/// reflection, no boxing beyond what EF Core's ValueConverter API requires.
/// </summary>
public sealed class DynamoEntityItemSerializerSource
{
    private readonly ConcurrentDictionary<IEntityType, EntityWritePlan> _cache = new();

    /// <summary>
    /// Returns the fully assembled DynamoDB item dictionary for a root <see cref="IUpdateEntry"/>.
    /// Owned sub-entries are resolved on-demand via the EF state manager, scoped to what is
    /// reachable from <paramref name="rootEntry"/> — no global owned-entries dictionary is needed.
    /// </summary>
    public Dictionary<string, AttributeValue> BuildItem(IUpdateEntry rootEntry)
        => GetOrBuildPlan(rootEntry.EntityType).Serialize(rootEntry, this);

    /// <summary>Returns the fully assembled DynamoDB item dictionary for an owned sub-entry.</summary>
    private Dictionary<string, AttributeValue> BuildItemFromOwnedEntry(IUpdateEntry entry)
        => GetOrBuildPlan(entry.EntityType).Serialize(entry, this);

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
                new ConvertedDictionaryFactory(conv, valueType));
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchType(property, setElementType, default(DirectSetFactory));
            EnsureSupportedSetProviderType(property, conv.ProviderClrType);
            return DispatchType(
                property,
                conv.ProviderClrType,
                new ConvertedSetFactory(conv, setElementType));
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var conv = typeMapping.ElementTypeMapping?.Converter;
            if (conv == null)
                return DispatchType(property, listElementType, default(DirectListFactory));
            EnsureSupportedValueProviderType(property, conv.ProviderClrType);
            return DispatchType(
                property,
                conv.ProviderClrType,
                new ConvertedListFactory(conv, listElementType));
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
    /// <remarks>
    /// <paramref name="type"/> is the <em>provider</em> CLR type — i.e., after any converter has
    /// been applied. Higher-level types such as <see cref="Guid"/>, <see cref="DateTime"/>, and
    /// <see cref="DateTimeOffset"/> are promoted to provider primitives (stored as <c>S</c>) via
    /// built-in EF Core type mappings, so they never reach this method directly.
    /// </remarks>
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
    ///     Casts <paramref name="providerValue" /> to <typeparamref name="TProvider" />, throwing an
    ///     <see cref="InvalidOperationException" /> with entity/property context if the cast fails. This
    ///     is only reachable if a <see cref="ValueConverter" /> returns a value whose runtime type does
    ///     not match the converter's declared <see cref="ValueConverter.ProviderClrType" />.
    /// </summary>
    private static TProvider CastProviderValue<TProvider>(object providerValue, IProperty property)
    {
        if (providerValue is TProvider typed)
            return typed;

        throw new InvalidOperationException(
            $"Property '{property.DeclaringType.DisplayName()}.{property.Name}': "
            + $"value converter returned '{providerValue.GetType().ShortDisplayName()}' "
            + $"but the declared provider type is '{typeof(TProvider).ShortDisplayName()}'. "
            + "Ensure the converter's ConvertToProvider delegate returns the correct type.");
    }

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
                    CastProviderValue<TProvider>(providerValue, property));
            };
        }
    }

    /// <summary>
    /// Converter-path factory for list properties. Dispatches on the model-side element type to
    /// use a fully typed <c>IEnumerable&lt;TElement&gt;</c> access path and a
    /// <c>Func&lt;TElement, TProvider&gt;</c> converter delegate — eliminating per-element boxing
    /// for the common element types. Falls back to a boxed path for unknown types such as
    /// user-defined enums or value objects.
    /// </summary>
    private readonly struct ConvertedListFactory(ValueConverter converter, Type elementClrType)
        : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = converter;
            if (TryCreateTyped<TProvider>(property, conv, elementClrType) is { } typed)
                return typed;

            // Fallback for element types outside the static dispatch table (e.g. user-defined
            // enums, value objects): accepts per-element boxing on both iteration and converter
            // call.
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

        /// <summary>
        ///     Attempts to produce a fully typed list delegate by pattern-matching
        ///     <paramref name="elementType" /> against the DynamoDB wire primitive types. Semantic types such
        ///     as <see cref="Guid" /> and <see cref="DateTime" /> are resolved to primitives by EF Core's own
        ///     type mapping infrastructure before reaching this dispatch, so they are not listed here. All
        ///     generic instantiations are statically visible to the AOT compiler. Returns
        ///     <see langword="null" /> for element types outside the primitive dispatch table (e.g.
        ///     user-defined enums, value objects), causing the caller to use the boxed fallback.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue>? TryCreateTyped<TProvider>(
            IProperty property,
            ValueConverter converter,
            Type elementType)
        {
            // The `converter is ValueConverter<TElement, TProvider>` pattern-match is both a safe
            // cast and a runtime guard: it handles EF Core wrapping nullable or derived converters.
            if (elementType == typeof(string) && converter is ValueConverter<string, TProvider> s)
                return CreateTyped(property, s);
            if (elementType == typeof(bool) && converter is ValueConverter<bool, TProvider> bl)
                return CreateTyped(property, bl);
            if (elementType == typeof(bool?) && converter is ValueConverter<bool?, TProvider> bln)
                return CreateTyped(property, bln);
            if (elementType == typeof(byte) && converter is ValueConverter<byte, TProvider> by)
                return CreateTyped(property, by);
            if (elementType == typeof(byte?) && converter is ValueConverter<byte?, TProvider> byn)
                return CreateTyped(property, byn);
            if (elementType == typeof(sbyte) && converter is ValueConverter<sbyte, TProvider> sb)
                return CreateTyped(property, sb);
            if (elementType == typeof(sbyte?) && converter is ValueConverter<sbyte?, TProvider> sbn)
                return CreateTyped(property, sbn);
            if (elementType == typeof(short) && converter is ValueConverter<short, TProvider> sh)
                return CreateTyped(property, sh);
            if (elementType == typeof(short?) && converter is ValueConverter<short?, TProvider> shn)
                return CreateTyped(property, shn);
            if (elementType == typeof(ushort) && converter is ValueConverter<ushort, TProvider> us)
                return CreateTyped(property, us);
            if (elementType == typeof(ushort?)
                && converter is ValueConverter<ushort?, TProvider> usn)
                return CreateTyped(property, usn);
            if (elementType == typeof(int) && converter is ValueConverter<int, TProvider> i)
                return CreateTyped(property, i);
            if (elementType == typeof(int?) && converter is ValueConverter<int?, TProvider> ni)
                return CreateTyped(property, ni);
            if (elementType == typeof(uint) && converter is ValueConverter<uint, TProvider> ui)
                return CreateTyped(property, ui);
            if (elementType == typeof(uint?) && converter is ValueConverter<uint?, TProvider> uin)
                return CreateTyped(property, uin);
            if (elementType == typeof(long) && converter is ValueConverter<long, TProvider> l)
                return CreateTyped(property, l);
            if (elementType == typeof(long?) && converter is ValueConverter<long?, TProvider> ln)
                return CreateTyped(property, ln);
            if (elementType == typeof(ulong) && converter is ValueConverter<ulong, TProvider> ul)
                return CreateTyped(property, ul);
            if (elementType == typeof(ulong?) && converter is ValueConverter<ulong?, TProvider> uln)
                return CreateTyped(property, uln);
            if (elementType == typeof(float) && converter is ValueConverter<float, TProvider> f)
                return CreateTyped(property, f);
            if (elementType == typeof(float?) && converter is ValueConverter<float?, TProvider> fn)
                return CreateTyped(property, fn);
            if (elementType == typeof(double) && converter is ValueConverter<double, TProvider> d)
                return CreateTyped(property, d);
            if (elementType == typeof(double?)
                && converter is ValueConverter<double?, TProvider> dn)
                return CreateTyped(property, dn);
            if (elementType == typeof(decimal)
                && converter is ValueConverter<decimal, TProvider> dec)
                return CreateTyped(property, dec);
            if (elementType == typeof(decimal?)
                && converter is ValueConverter<decimal?, TProvider> decn)
                return CreateTyped(property, decn);
            if (elementType == typeof(byte[]) && converter is ValueConverter<byte[], TProvider> ba)
                return CreateTyped(property, ba);
            return null;
        }

        /// <summary>
        ///     Builds the write delegate for the fully typed case. Captures
        ///     <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" /> once at plan-build time
        ///     — <c>ConvertToProviderTyped</c> compiles lazily and caches, so this is a one-time cost per
        ///     converter instance. The write-time hot path has no type dispatch, no reflection, and no boxing
        ///     on element access or converter call.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue> CreateTyped<TElement, TProvider>(
            IProperty property,
            ValueConverter<TElement, TProvider> converter)
        {
            var convDelegate = converter.ConvertToProviderTyped;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TElement>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers
                        .SerializeList<IEnumerable<TElement>, TElement, TProvider>(
                            value,
                            convDelegate);
            };
        }
    }

    /// <summary>
    /// Converter-path factory for set properties. Dispatches on the model-side element type to
    /// use a fully typed iteration path and <c>Func&lt;TElement, TProvider&gt;</c> converter,
    /// accumulating directly into SS/NS/BS without intermediate <see cref="AttributeValue"/>
    /// allocations. Falls back to a boxed path for unknown element types.
    /// </summary>
    private readonly struct ConvertedSetFactory(ValueConverter converter, Type elementClrType)
        : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = converter;
            if (TryCreateTyped<TProvider>(property, conv, elementClrType) is { } typed)
                return typed;

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

        /// <summary>
        ///     Same primitive dispatch table as <see cref="ConvertedListFactory" /> but produces a set
        ///     delegate. Semantic types are excluded for the same reason — EF Core resolves them to primitives
        ///     before this dispatch is reached. Returns <see langword="null" /> for element types outside the
        ///     primitive dispatch table.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue>? TryCreateTyped<TProvider>(
            IProperty property,
            ValueConverter converter,
            Type elementType)
        {
            if (elementType == typeof(string) && converter is ValueConverter<string, TProvider> s)
                return CreateTyped(property, s);
            if (elementType == typeof(bool) && converter is ValueConverter<bool, TProvider> bl)
                return CreateTyped(property, bl);
            if (elementType == typeof(bool?) && converter is ValueConverter<bool?, TProvider> bln)
                return CreateTyped(property, bln);
            if (elementType == typeof(byte) && converter is ValueConverter<byte, TProvider> by)
                return CreateTyped(property, by);
            if (elementType == typeof(byte?) && converter is ValueConverter<byte?, TProvider> byn)
                return CreateTyped(property, byn);
            if (elementType == typeof(sbyte) && converter is ValueConverter<sbyte, TProvider> sb)
                return CreateTyped(property, sb);
            if (elementType == typeof(sbyte?) && converter is ValueConverter<sbyte?, TProvider> sbn)
                return CreateTyped(property, sbn);
            if (elementType == typeof(short) && converter is ValueConverter<short, TProvider> sh)
                return CreateTyped(property, sh);
            if (elementType == typeof(short?) && converter is ValueConverter<short?, TProvider> shn)
                return CreateTyped(property, shn);
            if (elementType == typeof(ushort) && converter is ValueConverter<ushort, TProvider> us)
                return CreateTyped(property, us);
            if (elementType == typeof(ushort?)
                && converter is ValueConverter<ushort?, TProvider> usn)
                return CreateTyped(property, usn);
            if (elementType == typeof(int) && converter is ValueConverter<int, TProvider> i)
                return CreateTyped(property, i);
            if (elementType == typeof(int?) && converter is ValueConverter<int?, TProvider> ni)
                return CreateTyped(property, ni);
            if (elementType == typeof(uint) && converter is ValueConverter<uint, TProvider> ui)
                return CreateTyped(property, ui);
            if (elementType == typeof(uint?) && converter is ValueConverter<uint?, TProvider> uin)
                return CreateTyped(property, uin);
            if (elementType == typeof(long) && converter is ValueConverter<long, TProvider> l)
                return CreateTyped(property, l);
            if (elementType == typeof(long?) && converter is ValueConverter<long?, TProvider> ln)
                return CreateTyped(property, ln);
            if (elementType == typeof(ulong) && converter is ValueConverter<ulong, TProvider> ul)
                return CreateTyped(property, ul);
            if (elementType == typeof(ulong?) && converter is ValueConverter<ulong?, TProvider> uln)
                return CreateTyped(property, uln);
            if (elementType == typeof(float) && converter is ValueConverter<float, TProvider> f)
                return CreateTyped(property, f);
            if (elementType == typeof(float?) && converter is ValueConverter<float?, TProvider> fn)
                return CreateTyped(property, fn);
            if (elementType == typeof(double) && converter is ValueConverter<double, TProvider> d)
                return CreateTyped(property, d);
            if (elementType == typeof(double?)
                && converter is ValueConverter<double?, TProvider> dn)
                return CreateTyped(property, dn);
            if (elementType == typeof(decimal)
                && converter is ValueConverter<decimal, TProvider> dec)
                return CreateTyped(property, dec);
            if (elementType == typeof(decimal?)
                && converter is ValueConverter<decimal?, TProvider> decn)
                return CreateTyped(property, decn);
            if (elementType == typeof(byte[]) && converter is ValueConverter<byte[], TProvider> ba)
                return CreateTyped(property, ba);
            return null;
        }

        /// <summary>
        ///     Builds the write delegate for the fully typed set case. Captures
        ///     <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" /> once at plan-build time.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue> CreateTyped<TElement, TProvider>(
            IProperty property,
            ValueConverter<TElement, TProvider> converter)
        {
            var convDelegate = converter.ConvertToProviderTyped;
            return entry =>
            {
                var value = entry.GetCurrentValue<IEnumerable<TElement>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeSet(value, convDelegate);
            };
        }
    }

    /// <summary>
    ///     Converter-path factory for dictionary properties. Dispatches on the model-side value type
    ///     to use a fully typed <c>IEnumerable&lt;KeyValuePair&lt;string, TValue&gt;&gt;</c> access path
    ///     and a <c>Func&lt;TValue, TProvider&gt;</c> converter delegate, eliminating per-element boxing
    ///     for common value types. Falls back to a boxed path for unknown value types.
    /// </summary>
    private readonly struct ConvertedDictionaryFactory(ValueConverter converter, Type valueClrType)
        : ISerializerFactory
    {
        public Func<IUpdateEntry, AttributeValue> Create<TProvider>(IProperty property)
        {
            var conv = converter;
            if (TryCreateTyped<TProvider>(property, conv, valueClrType) is { } typed)
                return typed;

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

        /// <summary>
        ///     Same primitive dispatch table as <see cref="ConvertedListFactory" /> but dispatches on the
        ///     dictionary value type and produces a map delegate. Semantic types are excluded for the same
        ///     reason — EF Core resolves them to primitives before this dispatch is reached. Returns
        ///     <see langword="null" /> for value types outside the primitive dispatch table.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue>? TryCreateTyped<TProvider>(
            IProperty property,
            ValueConverter converter,
            Type valueType)
        {
            if (valueType == typeof(string) && converter is ValueConverter<string, TProvider> s)
                return CreateTyped(property, s);
            if (valueType == typeof(bool) && converter is ValueConverter<bool, TProvider> bl)
                return CreateTyped(property, bl);
            if (valueType == typeof(bool?) && converter is ValueConverter<bool?, TProvider> bln)
                return CreateTyped(property, bln);
            if (valueType == typeof(byte) && converter is ValueConverter<byte, TProvider> by)
                return CreateTyped(property, by);
            if (valueType == typeof(byte?) && converter is ValueConverter<byte?, TProvider> byn)
                return CreateTyped(property, byn);
            if (valueType == typeof(sbyte) && converter is ValueConverter<sbyte, TProvider> sb)
                return CreateTyped(property, sb);
            if (valueType == typeof(sbyte?) && converter is ValueConverter<sbyte?, TProvider> sbn)
                return CreateTyped(property, sbn);
            if (valueType == typeof(short) && converter is ValueConverter<short, TProvider> sh)
                return CreateTyped(property, sh);
            if (valueType == typeof(short?) && converter is ValueConverter<short?, TProvider> shn)
                return CreateTyped(property, shn);
            if (valueType == typeof(ushort) && converter is ValueConverter<ushort, TProvider> us)
                return CreateTyped(property, us);
            if (valueType == typeof(ushort?) && converter is ValueConverter<ushort?, TProvider> usn)
                return CreateTyped(property, usn);
            if (valueType == typeof(int) && converter is ValueConverter<int, TProvider> i)
                return CreateTyped(property, i);
            if (valueType == typeof(int?) && converter is ValueConverter<int?, TProvider> ni)
                return CreateTyped(property, ni);
            if (valueType == typeof(uint) && converter is ValueConverter<uint, TProvider> ui)
                return CreateTyped(property, ui);
            if (valueType == typeof(uint?) && converter is ValueConverter<uint?, TProvider> uin)
                return CreateTyped(property, uin);
            if (valueType == typeof(long) && converter is ValueConverter<long, TProvider> l)
                return CreateTyped(property, l);
            if (valueType == typeof(long?) && converter is ValueConverter<long?, TProvider> ln)
                return CreateTyped(property, ln);
            if (valueType == typeof(ulong) && converter is ValueConverter<ulong, TProvider> ul)
                return CreateTyped(property, ul);
            if (valueType == typeof(ulong?) && converter is ValueConverter<ulong?, TProvider> uln)
                return CreateTyped(property, uln);
            if (valueType == typeof(float) && converter is ValueConverter<float, TProvider> f)
                return CreateTyped(property, f);
            if (valueType == typeof(float?) && converter is ValueConverter<float?, TProvider> fn)
                return CreateTyped(property, fn);
            if (valueType == typeof(double) && converter is ValueConverter<double, TProvider> d)
                return CreateTyped(property, d);
            if (valueType == typeof(double?) && converter is ValueConverter<double?, TProvider> dn)
                return CreateTyped(property, dn);
            if (valueType == typeof(decimal) && converter is ValueConverter<decimal, TProvider> dec)
                return CreateTyped(property, dec);
            if (valueType == typeof(decimal?)
                && converter is ValueConverter<decimal?, TProvider> decn)
                return CreateTyped(property, decn);
            if (valueType == typeof(byte[]) && converter is ValueConverter<byte[], TProvider> ba)
                return CreateTyped(property, ba);
            return null;
        }

        /// <summary>
        ///     Builds the write delegate for the fully typed dictionary case. Captures
        ///     <see cref="ValueConverter{TModel,TProvider}.ConvertToProviderTyped" /> once at plan-build time.
        /// </summary>
        private static Func<IUpdateEntry, AttributeValue> CreateTyped<TValue, TProvider>(
            IProperty property,
            ValueConverter<TValue, TProvider> converter)
        {
            var convDelegate = converter.ConvertToProviderTyped;
            return entry =>
            {
                var value =
                    entry.GetCurrentValue<IEnumerable<KeyValuePair<string, TValue>>>(property);
                return value is null
                    ? NullAttributeValue()
                    : DynamoAttributeValueCollectionHelpers.SerializeDictionary(
                        value,
                        convDelegate);
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
