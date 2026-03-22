using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query;

/// <summary>Represents the DynamoQueryCompilationContext type.</summary>
public class DynamoQueryCompilationContext(
    QueryCompilationContextDependencies dependencies,
    bool async) : QueryCompilationContext(dependencies, async)
{
    /// <summary>
    ///     Set by <c>.WithNonKeyFilter()</c> translation. Removes the restriction that limits
    ///     <c>First*</c> to safe key-only query shapes. Does not change execution behavior.
    /// </summary>
    public bool NonKeyFilterAllowed { get; internal set; }

    /// <summary>
    ///     Per-query explicit secondary index name from <c>.WithIndex()</c>. The index name is
    ///     embedded in the PartiQL FROM clause at compile time, so it must be a compile-time constant.
    ///     The parameter is marked <c>[NotParameterized]</c> so EF Core's funcletizer leaves the
    ///     string literal as a <c>System.Linq.Expressions.ConstantExpression</c>; a
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
