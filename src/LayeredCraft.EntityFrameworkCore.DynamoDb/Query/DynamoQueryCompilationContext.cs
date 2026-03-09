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
    ///     Per-query explicit secondary index name from <c>.WithIndex()</c>. The parameter is marked
    ///     <c>[NotParameterized]</c> so EF Core's funcletizer leaves the string literal as a
    ///     <see cref="System.Linq.Expressions.ConstantExpression"/>, making it readable at translation time.
    /// </summary>
    public string? ExplicitIndexName { get; internal set; }
}
