using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Typed static helpers for serializing CLR collection values to DynamoDB
/// <see cref="AttributeValue"/> instances. These methods are called from within compiled
/// expression trees produced by <see cref="DynamoEntityItemSerializerSource"/> so that
/// collection serialization avoids non-generic <c>Cast&lt;object&gt;()</c> and IEnumerable boxing.
/// </summary>
/// <remarks>
/// Each method is generic over the element type (or value type for dictionaries) so the
/// JIT can generate type-specific code paths. The expression-tree builder calls
/// <c>MakeGenericMethod</c> once at compile time and caches the result in the compiled delegate.
/// </remarks>
internal static class DynamoAttributeValueCollectionHelpers
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  String set  →  SS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a <see cref="HashSet{T}"/> of strings to a DynamoDB string set (SS).
    /// Empty sets are represented as <c>{ NULL = true }</c> because DynamoDB forbids
    /// empty SS, NS, and BS attributes.
    /// </summary>
    public static AttributeValue SerializeStringSet(ISet<string> value)
        => value.Count == 0
            ? new AttributeValue { NULL = true }
            : new AttributeValue { SS = [..value] };

    // ──────────────────────────────────────────────────────────────────────────────
    //  Numeric set  →  NS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a set of numeric values to a DynamoDB numeric set (NS).
    /// DynamoDB NS elements must be string-encoded numbers. Empty sets are represented
    /// as <c>{ NULL = true }</c> for the same reason as <see cref="SerializeStringSet"/>.
    /// </summary>
    /// <typeparam name="T">The numeric element type (e.g. <c>int</c>, <c>decimal</c>).</typeparam>
    public static AttributeValue SerializeNumericSet<T>(ISet<T> value)
        where T : struct, IFormattable
        => value.Count == 0
            ? new AttributeValue { NULL = true }
            : new AttributeValue { NS = value.Select(static v => v.ToString(null, null)).ToList() };

    // ──────────────────────────────────────────────────────────────────────────────
    //  List  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a <see cref="List{T}"/> of scalar values to a DynamoDB list (L) where each
    /// element is a scalar <see cref="AttributeValue"/>. Empty lists are valid in DynamoDB
    /// and are serialized as <c>{ L = [] }</c>.
    /// </summary>
    /// <typeparam name="T">
    /// The element type. Must be a scalar type supported by
    /// <see cref="DynamoEntityItemSerializerSource"/>.
    /// </typeparam>
    public static AttributeValue SerializeList<T>(IList<T> value)
    {
        if (value.Count == 0)
            return new AttributeValue { L = [] };

        var elements = new List<AttributeValue>(value.Count);
        foreach (var item in value)
            elements.Add(SerializeListElement(item));
        return new AttributeValue { L = elements };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, V>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a <c>Dictionary&lt;string, V&gt;</c> to a DynamoDB map (M) where each value
    /// is a scalar <see cref="AttributeValue"/>. Empty dictionaries serialize to <c>{ M = {} }</c>.
    /// </summary>
    /// <typeparam name="TValue">
    /// The value type. Must be a scalar type supported by
    /// <see cref="DynamoEntityItemSerializerSource"/>.
    /// </typeparam>
    public static AttributeValue SerializeDictionary<TValue>(IDictionary<string, TValue> value)
    {
        if (value.Count == 0)
            return new AttributeValue { M = [] };

        var map = new Dictionary<string, AttributeValue>(value.Count);
        foreach (var (k, v) in value)
            map[k] = SerializeListElement(v);
        return new AttributeValue { M = map };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Shared scalar helper used by list and dictionary serializers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a single scalar element inside a list or dictionary value.
    /// Handles null elements and all scalar types supported by the provider.
    /// </summary>
    private static AttributeValue SerializeListElement<T>(T item)
    {
        // Null element → NULL attribute
        if (item is null)
            return new AttributeValue { NULL = true };

        // Pattern-match on the specific CLR type of the element.
        // The JIT specializes this per T, so no boxing for value types on the common path.
        return item switch
        {
            string s => new AttributeValue { S = s },
            bool b => new AttributeValue { BOOL = b },
            int i => new AttributeValue { N = i.ToString() },
            long l => new AttributeValue { N = l.ToString() },
            short sh => new AttributeValue { N = sh.ToString() },
            byte by => new AttributeValue { N = by.ToString() },
            double d => new AttributeValue { N = d.ToString("R") },
            float f => new AttributeValue { N = f.ToString("R") },
            decimal dec => new AttributeValue { N = dec.ToString() },
            Guid g => new AttributeValue { S = g.ToString() },
            DateTime dt => new AttributeValue { S = dt.ToString("O") },
            DateTimeOffset dto => new AttributeValue { S = dto.ToString("O") },
            _ => throw new NotSupportedException(
                $"Collection element type '{typeof(T).FullName}' has no DynamoDB AttributeValue "
                + "mapping. Add explicit support or use a value converter for this property."),
        };
    }
}
