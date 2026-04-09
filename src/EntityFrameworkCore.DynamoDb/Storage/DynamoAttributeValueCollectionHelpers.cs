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
    public static AttributeValue SerializeSet<TElement>(IEnumerable<TElement> value)
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
    public static AttributeValue SerializeSet<TElement, TProvider>(
        IEnumerable<TElement> value,
        Func<TElement, TProvider> convertToProvider)
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

    // ──────────────────────────────────────────────────────────────────────────────
    //  List  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a typed enumerable of scalar values to a DynamoDB list (L) where each
    /// element is a scalar <see cref="AttributeValue"/>. Empty lists are valid in DynamoDB
    /// and are serialized as <c>{ L = [] }</c>.
    /// </summary>
    /// <typeparam name="TElement">
    /// The element type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
    /// </typeparam>
    public static AttributeValue SerializeList<TElement>(IEnumerable<TElement> value)
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
            elements.Add(DynamoWireValueConversion.ConvertProviderValueToAttributeValue(item));
        return new AttributeValue { L = elements };
    }

    /// <summary>Serializes a typed enumerable to a DynamoDB list (L) applying a per-element converter.</summary>
    public static AttributeValue SerializeList<TElement, TProvider>(
        IEnumerable<TElement> value,
        Func<TElement, TProvider> convertToProvider)
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
            elements.Add(
                DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                    convertToProvider(item)));
        return new AttributeValue { L = elements };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, V>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes an enumerable of <c>KeyValuePair&lt;string, V&gt;</c> to a DynamoDB map (M) where
    /// each value is a scalar <see cref="AttributeValue"/>. Empty dictionaries serialize to
    /// <c>{ M = {} }</c>.
    /// </summary>
    /// <typeparam name="TValue">
    /// The value type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
    /// </typeparam>
    public static AttributeValue SerializeDictionary<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (k, v) in value)
            map[k] = DynamoWireValueConversion.ConvertProviderValueToAttributeValue(v);
        return new AttributeValue { M = map };
    }

    /// <summary>Serializes a typed dictionary to a DynamoDB map (M) applying a per-value converter.</summary>
    public static AttributeValue SerializeDictionary<TValue, TProvider>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        Func<TValue, TProvider> convertToProvider)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (k, v) in value)
            map[k] = DynamoWireValueConversion.ConvertProviderValueToAttributeValue(
                convertToProvider(v));
        return new AttributeValue { M = map };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Set accumulation helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Adds a single element to the appropriate set accumulator (SS, NS, or BS), enforcing that
    ///     all elements belong to the same DynamoDB set kind.
    /// </summary>
    internal static void AddSetElement<T>(
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

    /// <summary>
    ///     Assembles the final <see cref="AttributeValue" /> from whichever set accumulator is
    ///     populated. SS takes priority, then BS, then NS. At most one accumulator will be non-null
    ///     because <see cref="AddSetElement{T}" /> enforces kind homogeneity.
    /// </summary>
    internal static AttributeValue CreateSetAttributeValue(
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

    /// <summary>
    ///     Throws if elements of a different set kind (SS vs NS vs BS) have already been added, since
    ///     DynamoDB sets are homogeneous.
    /// </summary>
    private static void ThrowIfMixed(bool hasOtherKind, Type currentType)
    {
        if (hasOtherKind)
            throw new InvalidOperationException(
                $"DynamoDB sets must be homogeneous (SS, NS, or BS — not mixed). "
                + $"Encountered '{currentType.Name}' but elements of a different kind were already added. "
                + "Ensure all elements in the set map to the same provider type.");
    }
}
