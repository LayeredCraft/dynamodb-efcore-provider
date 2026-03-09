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
    ///     Per-query explicit secondary index name when the argument to <c>.WithIndex()</c> was
    ///     still a <see cref="System.Linq.Expressions.ConstantExpression"/> at translation time
    ///     (e.g. <c>EF.Constant("ByStatus")</c>). Null in the common case — use
    ///     <see cref="ExplicitIndexNameExpression"/> instead.
    /// </summary>
    public string? ExplicitIndexName { get; internal set; }

    /// <summary>
    ///     Expression for the index name argument captured during translation. In normal queries
    ///     EF Core's <c>ExpressionTreeFuncletizer</c> runs before translation and converts the
    ///     string literal (e.g. <c>"ByStatus"</c>) into a
    ///     <see cref="Microsoft.EntityFrameworkCore.Query.QueryParameterExpression" />, so this
    ///     field holds that node rather than a <see cref="System.Linq.Expressions.ConstantExpression" />.
    ///     The actual string value is retrieved at SQL-generation time from
    ///     <c>queryContext.Parameters</c> via <c>DynamoQuerySqlGenerator.ResolveIndexName</c>.
    /// </summary>
    public Expression? ExplicitIndexNameExpression { get; internal set; }
}
