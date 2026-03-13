using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query;

public class DynamoQueryCompilationContext(
    QueryCompilationContextDependencies dependencies,
    bool async) : QueryCompilationContext(dependencies, async)
{
    /// <summary>
    ///     Per-query override for page size (from .WithPageSize() extension). Null means use global
    ///     default.
    /// </summary>
    public int? PageSizeOverride { get; internal set; }

    public Expression? PageSizeOverrideExpression { get; internal set; }

    /// <summary>Per-query flag to disable pagination continuation (from .WithoutPagination() extension).</summary>
    public bool PaginationDisabled { get; internal set; }

    /// <summary>
    ///     Per-query explicit secondary index name from <c>.WithIndex()</c>. The index name is
    ///     embedded in the PartiQL FROM clause at compile time, so it must be a compile-time constant.
    ///     The parameter is marked <c>[NotParameterized]</c> so EF Core's funcletizer leaves the
    ///     string literal as a <see cref="System.Linq.Expressions.ConstantExpression"/>; a
    ///     non-constant argument causes translation to throw rather than silently fall back to the
    ///     base table.
    /// </summary>
    public string? ExplicitIndexName { get; internal set; }

    /// <summary>
    ///     Per-query flag set by <c>.WithoutIndex()</c>. When <c>true</c>, index selection is
    ///     suppressed and the query executes against the base table regardless of the configured
    ///     automatic index selection mode. Combining this with <c>.WithIndex()</c> on the same
    ///     query throws at compile time.
    /// </summary>
    public bool IndexSelectionDisabled { get; internal set; }
}
