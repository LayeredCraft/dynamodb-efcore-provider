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

    /// <summary>
    ///     Serializes a typed enumerable to a DynamoDB set (SS/NS/BS). Delegates to
    ///     <see cref="SerializeSet{TElement,TProvider}" /> with an identity converter; the JIT inlines the
    ///     identity and produces equivalent code to a direct typed path.
    /// </summary>
    public static AttributeValue SerializeSet<TElement>(IEnumerable<TElement> value)
        => SerializeSet(value, static x => x);

    /// <summary>
    ///     Serializes a typed enumerable to a DynamoDB set (SS/NS/BS) applying a per-element
    ///     converter from <typeparamref name="TElement"/> to <typeparamref name="TProvider"/>.
    ///     The set kind (SS, NS, or BS) is resolved from <typeparamref name="TProvider"/> once
    ///     at JIT time — the <c>typeof</c> checks fold to constants per instantiation so no
    ///     per-element type dispatch is performed.
    /// </summary>
    public static AttributeValue SerializeSet<TElement, TProvider>(
        IEnumerable<TElement> value,
        Func<TElement, TProvider> convertToProvider)
    {
        if (typeof(TProvider) == typeof(string))
        {
            var ss = new List<string>();
            foreach (var item in value)
            {
                var converted = convertToProvider(item);
                if (converted is null)
                    throw new InvalidOperationException(
                        "DynamoDB sets cannot contain null elements.");
                ss.Add((string)(object)converted);
            }

            return ss.Count == 0
                ? new AttributeValue { NULL = true }
                : new AttributeValue { SS = ss };
        }

        if (typeof(TProvider) == typeof(byte[]))
        {
            var bs = new List<MemoryStream>();
            foreach (var item in value)
            {
                var converted = convertToProvider(item);
                if (converted is null)
                    throw new InvalidOperationException(
                        "DynamoDB sets cannot contain null elements.");
                bs.Add(new MemoryStream((byte[])(object)converted, false));
            }

            return bs.Count == 0
                ? new AttributeValue { NULL = true }
                : new AttributeValue { BS = bs };
        }

        // TProvider is a numeric wire type. FormatNumberSetElement<TProvider> is generic and its
        // internal switch is a compile-time constant per JIT instantiation — one branch per
        // numeric type, no runtime dispatch.
        var ns = new List<string>();
        foreach (var item in value)
        {
            var converted = convertToProvider(item);
            if (converted is null)
                throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");
            ns.Add(FormatNumberSetElement(converted));
        }

        return ns.Count == 0 ? new AttributeValue { NULL = true } : new AttributeValue { NS = ns };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  List  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Serializes a typed enumerable of scalar values to a DynamoDB list (L) where each
    ///     element is a scalar <see cref="AttributeValue"/>. Empty lists are valid in DynamoDB
    ///     and are serialized as <c>{ L = [] }</c>.
    /// </summary>
    /// <typeparam name="TElement">
    ///     The element type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
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

    /// <summary>
    ///     Serializes a typed enumerable to a DynamoDB list (L) using a per-element
    ///     <see cref="AttributeValue" /> factory.
    /// </summary>
    public static AttributeValue SerializeList<TElement>(
        IEnumerable<TElement> value,
        Func<TElement, AttributeValue> serializeElement)
    {
        var elements = new List<AttributeValue>();
        foreach (var item in value)
            elements.Add(serializeElement(item));
        return new AttributeValue { L = elements };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, V>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Serializes an enumerable of <c>KeyValuePair&lt;string, V&gt;</c> to a DynamoDB map (M)
    ///     where each value is a scalar <see cref="AttributeValue"/>. Empty dictionaries serialize
    ///     to <c>{ M = {} }</c>.
    /// </summary>
    /// <typeparam name="TValue">
    ///     The value type. Must be a scalar type supported by <see cref="DynamoWireValueConversion"/>.
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

    /// <summary>
    ///     Serializes a typed dictionary to a DynamoDB map (M) using a per-value
    ///     <see cref="AttributeValue" /> factory.
    /// </summary>
    public static AttributeValue SerializeDictionary<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        Func<TValue, AttributeValue> serializeValue)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (k, v) in value)
            map[k] = serializeValue(v);
        return new AttributeValue { M = map };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Formats a single numeric value as a DynamoDB number string. Since
    ///     <typeparamref name="T"/> is known at JIT time, the switch compiles to a single
    ///     unconditional case per instantiation — no runtime dispatch overhead.
    /// </summary>
    private static string FormatNumberSetElement<T>(T item)
        => item switch
        {
            byte by => DynamoWireValueConversion.FormatNumber(by),
            sbyte sb => DynamoWireValueConversion.FormatNumber(sb),
            short sh => DynamoWireValueConversion.FormatNumber(sh),
            ushort ush => DynamoWireValueConversion.FormatNumber(ush),
            int i => DynamoWireValueConversion.FormatNumber(i),
            uint ui => DynamoWireValueConversion.FormatNumber(ui),
            long l => DynamoWireValueConversion.FormatNumber(l),
            ulong ul => DynamoWireValueConversion.FormatNumber(ul),
            float f => DynamoWireValueConversion.FormatNumber(f),
            double d => DynamoWireValueConversion.FormatNumber(d),
            decimal dec => DynamoWireValueConversion.FormatNumber(dec),
            _ => throw new NotSupportedException(
                $"DynamoDB set element type '{typeof(T).FullName}' is not supported. "
                + "Configure a converter to string, number, or byte[]."),
        };
}
