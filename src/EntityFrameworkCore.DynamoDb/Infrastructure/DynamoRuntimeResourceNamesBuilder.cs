using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;

namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>
///     Maps logical table and index identities to the environment-specific physical resource names
///     (DynamoDB table and secondary-index names) used at runtime.
/// </summary>
/// <remarks>
///     <para>
///         A table's <b>logical table identity</b> is declared in the model with
///         <c>HasLogicalTableName</c>. A secondary index's <b>logical index identity</b> is its EF
///         index name, the name passed to <c>HasGlobalSecondaryIndex</c> or
///         <c>HasLocalSecondaryIndex</c>. The <b>physical resource name</b> of each is the real DynamoDB
///         name, which <c>ToTable</c> and <c>HasSecondaryIndexName</c> configure in the model and this
///         builder overrides at runtime.
///     </para>
///     <para>
///         This is an advanced facility for compiled models (for example Native AOT applications
///         promoted unchanged between environments with different physical names). It is not the
///         recommended way to configure resource names in general. The mappings are applied once, when
///         the model is initialized, and are fixed for the lifetime of that model; they are not for
///         per-request table switching or tenant routing.
///     </para>
/// </remarks>
public sealed class DynamoRuntimeResourceNamesBuilder
{
    private readonly Dictionary<string, string> _tables = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<string, string>> _indexes =
        new(StringComparer.Ordinal);

    /// <summary>Maps a logical table identity to its physical DynamoDB table name.</summary>
    /// <param name="logicalTable">The logical table identity declared with <c>HasLogicalTableName</c>.</param>
    /// <param name="physicalName">The physical DynamoDB table name to use at runtime.</param>
    /// <returns>The same builder so that calls can be chained.</returns>
    /// <exception cref="ArgumentException">A name is null or empty.</exception>
    /// <exception cref="InvalidOperationException">The logical table identity is already mapped.</exception>
    public DynamoRuntimeResourceNamesBuilder Table(string logicalTable, string physicalName)
    {
        logicalTable.NotEmpty();
        physicalName.NotEmpty();

        if (!_tables.TryAdd(logicalTable, physicalName))
            throw new InvalidOperationException(
                $"The logical table '{logicalTable}' is mapped more than once.");

        return this;
    }

    /// <summary>Maps a logical index identity to its physical DynamoDB secondary-index name.</summary>
    /// <param name="logicalTable">The logical table identity of the table that owns the index.</param>
    /// <param name="logicalIndex">
    ///     The logical index identity: the EF index name passed to <c>HasGlobalSecondaryIndex</c> or
    ///     <c>HasLocalSecondaryIndex</c>.
    /// </param>
    /// <param name="physicalName">The physical DynamoDB index name to use at runtime.</param>
    /// <returns>The same builder so that calls can be chained.</returns>
    /// <exception cref="ArgumentException">A name is null or empty.</exception>
    /// <exception cref="InvalidOperationException">The logical index identity is already mapped.</exception>
    public DynamoRuntimeResourceNamesBuilder SecondaryIndex(
        string logicalTable,
        string logicalIndex,
        string physicalName)
    {
        logicalTable.NotEmpty();
        logicalIndex.NotEmpty();
        physicalName.NotEmpty();

        if (!_indexes.TryGetValue(logicalTable, out var indexes))
            _indexes[logicalTable] = indexes = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!indexes.TryAdd(logicalIndex, physicalName))
            throw new InvalidOperationException(
                $"The logical secondary index '{logicalIndex}' of table '{logicalTable}' is mapped more than once.");

        return this;
    }

    internal DynamoRuntimeResourceNames Build()
        => new(
            new Dictionary<string, string>(_tables, StringComparer.Ordinal),
            _indexes.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(
                    pair.Value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal));
}
