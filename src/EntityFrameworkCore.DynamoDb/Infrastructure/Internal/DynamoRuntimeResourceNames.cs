namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>
///     Immutable, value-comparable mapping from logical DynamoDB resource identities to the
///     environment-specific physical names configured at runtime.
/// </summary>
/// <remarks>
///     This is provider infrastructure, not a consumer contract; consumers configure it through
///     <see cref="DynamoDbContextOptionsBuilder.RuntimeResourceNames" />. The type is public only
///     because it is exposed by <see cref="IDynamoSingletonOptions" />, which the model initializer's
///     public constructor requires; its members are internal. It participates in the EF internal
///     service-provider key, so equal mappings share a service provider and a model.
/// </remarks>
public sealed class DynamoRuntimeResourceNames : IEquatable<DynamoRuntimeResourceNames>
{
    private readonly int _hashCode;

    internal DynamoRuntimeResourceNames(
        IReadOnlyDictionary<string, string> tables,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> secondaryIndexes)
    {
        Tables = tables;
        SecondaryIndexes = secondaryIndexes;

        var hash = new HashCode();
        foreach (var (logicalTable, physicalName) in tables.OrderBy(
            static pair => pair.Key,
            StringComparer.Ordinal))
        {
            hash.Add(logicalTable, StringComparer.Ordinal);
            hash.Add(physicalName, StringComparer.Ordinal);
        }

        foreach (var (logicalTable, indexes) in secondaryIndexes.OrderBy(
            static pair => pair.Key,
            StringComparer.Ordinal))
        {
            hash.Add(logicalTable, StringComparer.Ordinal);
            foreach (var (logicalIndex, physicalName) in indexes.OrderBy(
                static pair => pair.Key,
                StringComparer.Ordinal))
            {
                hash.Add(logicalIndex, StringComparer.Ordinal);
                hash.Add(physicalName, StringComparer.Ordinal);
            }
        }

        _hashCode = hash.ToHashCode();
    }

    /// <summary>Physical table names keyed by logical table identity.</summary>
    internal IReadOnlyDictionary<string, string> Tables { get; }

    /// <summary>Physical index names keyed by logical table identity, then logical index identity.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SecondaryIndexes
    {
        get;
    }

    /// <inheritdoc />
    public bool Equals(DynamoRuntimeResourceNames? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null
            || _hashCode != other._hashCode
            || Tables.Count != other.Tables.Count
            || SecondaryIndexes.Count != other.SecondaryIndexes.Count)
            return false;

        foreach (var (logicalTable, physicalName) in Tables)
            if (!other.Tables.TryGetValue(logicalTable, out var otherName)
                || !string.Equals(physicalName, otherName, StringComparison.Ordinal))
                return false;

        foreach (var (logicalTable, indexes) in SecondaryIndexes)
        {
            if (!other.SecondaryIndexes.TryGetValue(logicalTable, out var otherIndexes)
                || indexes.Count != otherIndexes.Count)
                return false;

            foreach (var (logicalIndex, physicalName) in indexes)
                if (!otherIndexes.TryGetValue(logicalIndex, out var otherName)
                    || !string.Equals(physicalName, otherName, StringComparison.Ordinal))
                    return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DynamoRuntimeResourceNames);

    /// <inheritdoc />
    public override int GetHashCode() => _hashCode;
}
