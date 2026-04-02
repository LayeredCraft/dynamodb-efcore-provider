using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace EntityFrameworkCore.DynamoDb.Update.Internal;

/// <summary>
///     The output of <see cref="InsertExpressionBuilder.Build" />: an expression tree
///     together with the runtime parameter values needed to generate a PartiQL INSERT statement.
/// </summary>
/// <param name="Expression">The INSERT expression tree.</param>
/// <param name="ParameterValues">
///     A dictionary mapping parameter names (e.g. <c>p0</c>, <c>p1</c>) to their runtime values.
/// </param>
public sealed record InsertStatementPlan(
    InsertExpression Expression,
    IReadOnlyDictionary<string, object?> ParameterValues);
