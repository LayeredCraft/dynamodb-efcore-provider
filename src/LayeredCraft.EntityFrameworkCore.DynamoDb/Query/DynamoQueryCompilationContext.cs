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

    /// <summary>Per-query explicit secondary index selection (from .WithIndex() extension).</summary>
    public string? ExplicitIndexName { get; internal set; }

    /// <summary>
    ///     Expression for the index name argument, captured after EF Core parameter extraction.
    ///     May be a <see cref="Microsoft.EntityFrameworkCore.Query.QueryParameterExpression" /> when
    ///     the index name was a runtime-extracted constant, or a <see cref="System.Linq.Expressions.ConstantExpression" />
    ///     when it remained inline.
    /// </summary>
    public Expression? ExplicitIndexNameExpression { get; internal set; }
}
