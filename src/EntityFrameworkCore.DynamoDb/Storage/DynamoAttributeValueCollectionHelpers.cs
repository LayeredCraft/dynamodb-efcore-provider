using System.Collections;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Typed static helpers for serializing CLR collection values to DynamoDB
/// <see cref="AttributeValue"/> instances. These methods are called from the cached
/// write-plan delegates produced by <see cref="DynamoEntityItemSerializerSource"/> so that
/// collection serialization stays on typed generic code paths.
/// </summary>
internal static class DynamoAttributeValueCollectionHelpers
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Set  →  SS / NS / BS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Serializes a typed enumerable to a DynamoDB set (SS/NS/BS).</summary>
    public static AttributeValue SerializeSet<TCollection, TElement>(TCollection value)
        where TCollection : IEnumerable<TElement>
    {
        List<string>? stringSet = null;
        List<string>? numberSet = null;
        List<MemoryStream>? binarySet = null;
        var hasElements = false;

        foreach (var item in value)
        {
            hasElements = true;
            AddSetElement(item, ref stringSet, ref numberSet, ref binarySet);
        }

        if (!hasElements)
            return new AttributeValue { NULL = true };

        return CreateSetAttributeValue(stringSet, numberSet, binarySet);
    }

    /// <summary>
    /// Serializes a typed enumerable to a DynamoDB set (SS/NS/BS) applying a per-element
    /// converter from <typeparamref name="TElement"/> to <typeparamref name="TProvider"/>.
    /// </summary>
    public static AttributeValue SerializeSet<TCollection, TElement, TProvider>(
        TCollection value,
        Func<TElement, TProvider> convertToProvider) where TCollection : IEnumerable<TElement>
    {
        List<string>? stringSet = null;
        List<string>? numberSet = null;
        List<MemoryStream>? binarySet = null;
        var hasElements = false;

        foreach (var item in value)
        {
            hasElements = true;
            AddSetElement(convertToProvider(item), ref stringSet, ref numberSet, ref binarySet);
        }

        if (!hasElements)
            return new AttributeValue { NULL = true };

        return CreateSetAttributeValue(stringSet, numberSet, binarySet);
    }

    /// <summary>
    /// Serializes a non-generic enumerable of model values to a DynamoDB set (SS/NS/BS) using a
    /// boxed provider converter. The provider type <typeparamref name="TProvider"/> is resolved at
    /// plan-build time via <c>DispatchSupportedDirectType</c> so that
    /// <see cref="AddSetElement{T}"/> is JIT-specialized per provider type rather than dispatched
    /// via a boxed pattern-match on every element.
    /// </summary>
    /// <param name="value">Non-generic enumerable of model-side elements.</param>
    /// <param name="convertToProvider">
    /// The <see cref="Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.ConvertToProvider"/>
    /// delegate; accepts <c>object?</c> and returns <c>object?</c>.
    /// </param>
    public static AttributeValue SerializeSet<TProvider>(
        IEnumerable value,
        Func<object?, object?> convertToProvider)
    {
        List<string>? stringSet = null;
        List<string>? numberSet = null;
        List<MemoryStream>? binarySet = null;
        var hasElements = false;

        foreach (var item in value)
        {
            hasElements = true;
            var providerValue = convertToProvider(item);
            if (providerValue is null)
                throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");
            // Cast unboxes to TProvider; AddSetElement<TProvider> is JIT-specialized so the
            // internal switch resolves to a single branch at runtime.
            AddSetElement((TProvider)providerValue, ref stringSet, ref numberSet, ref binarySet);
        }

        if (!hasElements)
            return new AttributeValue { NULL = true };

        return CreateSetAttributeValue(stringSet, numberSet, binarySet);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  List  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a <see cref="List{T}"/> of scalar values to a DynamoDB list (L) where each
    /// element is a scalar <see cref="AttributeValue"/>. Empty lists are valid in DynamoDB
    /// and are serialized as <c>{ L = [] }</c>.
    /// </summary>
    /// <typeparam name="TCollection">The concrete collection type.</typeparam>
    /// <typeparam name="TElement">
    /// The element type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
    /// </typeparam>
    public static AttributeValue SerializeList<TCollection, TElement>(TCollection value)
        where TCollection : IEnumerable<TElement>
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
            elements.Add(SerializeElement(item));
        return new AttributeValue { L = elements };
    }

    /// <summary>Serializes a typed enumerable to a DynamoDB list (L) applying a per-element converter.</summary>
    public static AttributeValue SerializeList<TCollection, TElement, TProvider>(
        TCollection value,
        Func<TElement, TProvider> convertToProvider) where TCollection : IEnumerable<TElement>
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
            elements.Add(SerializeElement(convertToProvider(item)));
        return new AttributeValue { L = elements };
    }

    /// <summary>
    ///     Serializes a non-generic enumerable of model values to a DynamoDB list (L) using a boxed
    ///     provider converter. Null provider results become <c>{ NULL = true }</c> elements. The provider
    ///     type <typeparamref name="TProvider" /> is resolved at plan-build time so that
    ///     <see cref="DynamoWireValueConversion.ConvertProviderValueToAttributeValue{T}" /> is
    ///     JIT-specialized per provider type.
    /// </summary>
    /// <param name="value">Non-generic enumerable of model-side elements.</param>
    /// <param name="convertToProvider">
    ///     The
    ///     <see
    ///         cref="Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.ConvertToProvider" />
    ///     delegate; accepts <c>object?</c> and returns <c>object?</c>.
    /// </param>
    public static AttributeValue SerializeList<TProvider>(
        IEnumerable value,
        Func<object?, object?> convertToProvider)
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
        {
            var providerValue = convertToProvider(item);
            elements.Add(
                providerValue is null
                    ? new AttributeValue { NULL = true }
                    : DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                        (TProvider)providerValue));
        }

        return new AttributeValue { L = elements };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, V>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a <c>Dictionary&lt;string, V&gt;</c> to a DynamoDB map (M) where each value
    /// is a scalar <see cref="AttributeValue"/>. Empty dictionaries serialize to <c>{ M = {} }</c>.
    /// </summary>
    /// <typeparam name="TCollection">The concrete dictionary shape.</typeparam>
    /// <typeparam name="TValue">
    /// The value type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
    /// </typeparam>
    public static AttributeValue SerializeDictionary<TCollection, TValue>(TCollection value)
        where TCollection : IEnumerable<KeyValuePair<string, TValue>>
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (k, v) in value)
            map[k] = SerializeElement(v);
        return new AttributeValue { M = map };
    }

    /// <summary>Serializes a typed dictionary to a DynamoDB map (M) applying a per-value converter.</summary>
    public static AttributeValue SerializeDictionary<TCollection, TValue, TProvider>(
        TCollection value,
        Func<TValue, TProvider> convertToProvider)
        where TCollection : IEnumerable<KeyValuePair<string, TValue>>
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (k, v) in value)
            map[k] = SerializeElement(convertToProvider(v));
        return new AttributeValue { M = map };
    }

    /// <summary>
    ///     Serializes a non-generic <see cref="IDictionary" /> of model values to a DynamoDB map (M)
    ///     using a boxed provider converter. Null provider results become <c>{ NULL = true }</c> values.
    ///     The provider type <typeparamref name="TProvider" /> is resolved at plan-build time so that
    ///     <see cref="DynamoWireValueConversion.ConvertProviderValueToAttributeValue{T}" /> is
    ///     JIT-specialized per provider type.
    /// </summary>
    /// <param name="value">Non-generic dictionary with string keys and model-side values.</param>
    /// <param name="convertToProvider">
    ///     The
    ///     <see
    ///         cref="Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.ConvertToProvider" />
    ///     delegate; accepts <c>object?</c> and returns <c>object?</c>.
    /// </param>
    public static AttributeValue SerializeDictionary<TProvider>(
        IDictionary value,
        Func<object?, object?> convertToProvider)
    {
        var map = new Dictionary<string, AttributeValue>(value.Count, StringComparer.Ordinal);
        foreach (DictionaryEntry entry in value)
        {
            if (entry.Key is not string key)
                throw new InvalidOperationException(
                    "DynamoDB dictionary keys must be strings on the write path.");
            var providerValue = convertToProvider(entry.Value);
            map[key] = providerValue is null
                ? new AttributeValue { NULL = true }
                : DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    (TProvider)providerValue);
        }

        return new AttributeValue { M = map };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Shared scalar helper used by list and dictionary serializers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a single scalar element inside a list or dictionary value.
    /// Handles null elements and all scalar types supported by the provider.
    /// </summary>
    private static AttributeValue SerializeElement<T>(T item)
        => DynamoWireValueConversion.ConvertProviderValueToAttributeValue(item);

    private static void AddSetElement<T>(
        T item,
        ref List<string>? stringSet,
        ref List<string>? numberSet,
        ref List<MemoryStream>? binarySet)
    {
        if (item is null)
            throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");

        switch (item)
        {
            case string s:
                ThrowIfMixed(numberSet != null || binarySet != null, typeof(string));
                (stringSet ??= []).Add(s);
                return;
            case byte[] bytes:
                ThrowIfMixed(stringSet != null || numberSet != null, typeof(byte[]));
                (binarySet ??= []).Add(new MemoryStream(bytes, false));
                return;
            case byte by:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(byte));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(by));
                return;
            case sbyte sb:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(sbyte));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(sb));
                return;
            case short sh:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(short));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(sh));
                return;
            case ushort ush:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(ushort));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(ush));
                return;
            case int i:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(int));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(i));
                return;
            case uint ui:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(uint));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(ui));
                return;
            case long l:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(long));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(l));
                return;
            case ulong ul:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(ulong));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(ul));
                return;
            case float f:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(float));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(f));
                return;
            case double d:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(double));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(d));
                return;
            case decimal dec:
                ThrowIfMixed(stringSet != null || binarySet != null, typeof(decimal));
                (numberSet ??= []).Add(DynamoWireValueConversion.FormatNumber(dec));
                return;
            default:
                throw new NotSupportedException(
                    $"DynamoDB set element type '{typeof(T).FullName}' is not supported. Configure a converter to string, number, or byte[].");
        }
    }

    private static AttributeValue CreateSetAttributeValue(
        List<string>? stringSet,
        List<string>? numberSet,
        List<MemoryStream>? binarySet)
    {
        if (stringSet != null)
            return new AttributeValue { SS = stringSet };

        if (binarySet != null)
            return new AttributeValue { BS = binarySet };

        return new AttributeValue { NS = numberSet ?? [] };
    }

    private static void ThrowIfMixed(bool hasOtherKind, Type currentType)
    {
        if (hasOtherKind)
            throw new InvalidOperationException(
                $"DynamoDB sets cannot mix provider element kinds. Encountered '{currentType.Name}'.");
    }
}
