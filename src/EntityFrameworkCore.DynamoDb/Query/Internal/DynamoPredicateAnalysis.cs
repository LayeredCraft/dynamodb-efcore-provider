using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Shared predicate-analysis helpers used by query post-processing and ExecuteUpdate
///     translation validation.
/// </summary>
internal static class DynamoPredicateAnalysis
{
    /// <summary>
    ///     Returns <c>true</c> when the predicate references any attribute that is not the effective
    ///     partition key or sort key of the active query source.
    /// </summary>
    public static bool HasNonKeyPredicates(
        SqlExpression predicate,
        string effectivePk,
        string? effectiveSortKey)
    {
        var keyAttrs = new HashSet<string>(StringComparer.Ordinal) { effectivePk };
        if (effectiveSortKey is not null)
            keyAttrs.Add(effectiveSortKey);

        return ContainsNonKeyProperty(predicate, keyAttrs);
    }

    /// <summary>
    ///     Recursively walks a SQL predicate expression and returns <c>true</c> if any
    ///     <see cref="SqlPropertyExpression" /> references the given <paramref name="attributeName" />.
    /// </summary>
    public static bool PredicateReferencesAttribute(SqlExpression expression, string attributeName)
        => expression switch
        {
            SqlPropertyExpression prop => prop.PropertyName == attributeName,
            SqlBinaryExpression bin => PredicateReferencesAttribute(bin.Left, attributeName)
                || PredicateReferencesAttribute(bin.Right, attributeName),
            SqlUnaryExpression unary => PredicateReferencesAttribute(unary.Operand, attributeName),
            SqlIsNullExpression isNull => PredicateReferencesAttribute(
                isNull.Operand,
                attributeName),
            SqlParenthesizedExpression paren => PredicateReferencesAttribute(
                paren.Operand,
                attributeName),
            SqlBetweenExpression between => PredicateReferencesAttribute(
                between.Subject,
                attributeName),
            SqlFunctionExpression func => func.Arguments.Any(a
                => PredicateReferencesAttribute(a, attributeName)),
            SqlInExpression inExpr => PredicateReferencesAttribute(inExpr.Item, attributeName),
            // Recurse into nested-path and list-index chains so callers searching for a top-level
            // attribute name (e.g. the sort-key attribute) can match the root
            // SqlPropertyExpression.
            DynamoScalarAccessExpression scalar => scalar.Parent is SqlExpression parentSql
                && PredicateReferencesAttribute(parentSql, attributeName),
            DynamoListIndexExpression listIdx => listIdx.Source is SqlExpression sourceSql
                && PredicateReferencesAttribute(sourceSql, attributeName),
            _ => false
        };

    /// <summary>Returns <c>true</c> when any OR branch references the active PK or SK.</summary>
    public static bool PredicateHasOrTouchingKey(
        SqlExpression expression,
        string effectivePk,
        string? effectiveSortKey)
        => expression switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } or =>
                PredicateReferencesAttribute(or.Left, effectivePk)
                || PredicateReferencesAttribute(or.Right, effectivePk)
                || (effectiveSortKey is not null
                    && (PredicateReferencesAttribute(or.Left, effectiveSortKey)
                        || PredicateReferencesAttribute(or.Right, effectiveSortKey)))
                || PredicateHasOrTouchingKey(or.Left, effectivePk, effectiveSortKey)
                || PredicateHasOrTouchingKey(or.Right, effectivePk, effectiveSortKey),
            SqlBinaryExpression bin => PredicateHasOrTouchingKey(
                    bin.Left,
                    effectivePk,
                    effectiveSortKey)
                || PredicateHasOrTouchingKey(bin.Right, effectivePk, effectiveSortKey),
            SqlUnaryExpression unary => PredicateHasOrTouchingKey(
                unary.Operand,
                effectivePk,
                effectiveSortKey),
            SqlIsNullExpression isNull => PredicateHasOrTouchingKey(
                isNull.Operand,
                effectivePk,
                effectiveSortKey),
            SqlParenthesizedExpression paren => PredicateHasOrTouchingKey(
                paren.Operand,
                effectivePk,
                effectiveSortKey),
            SqlBetweenExpression between => PredicateHasOrTouchingKey(
                between.Subject,
                effectivePk,
                effectiveSortKey),
            SqlFunctionExpression func => func.Arguments.Any(a
                => PredicateHasOrTouchingKey(a, effectivePk, effectiveSortKey)),
            SqlInExpression inExpr => PredicateHasOrTouchingKey(
                inExpr.Item,
                effectivePk,
                effectiveSortKey),
            DynamoScalarAccessExpression scalar => scalar.Parent is SqlExpression parentSql
                && PredicateHasOrTouchingKey(parentSql, effectivePk, effectiveSortKey),
            DynamoListIndexExpression listIdx => listIdx.Source is SqlExpression sourceSql
                && PredicateHasOrTouchingKey(sourceSql, effectivePk, effectiveSortKey),
            _ => false
        };

    /// <summary>
    ///     Returns <c>true</c> when the predicate tree contains a provider-injected discriminator
    ///     predicate node.
    /// </summary>
    public static bool ContainsDiscriminatorPredicate(SqlExpression expression)
        => expression switch
        {
            SqlDiscriminatorPredicateExpression => true,
            SqlBinaryExpression bin => ContainsDiscriminatorPredicate(bin.Left)
                || ContainsDiscriminatorPredicate(bin.Right),
            SqlUnaryExpression unary => ContainsDiscriminatorPredicate(unary.Operand),
            SqlIsNullExpression isNull => ContainsDiscriminatorPredicate(isNull.Operand),
            SqlParenthesizedExpression paren => ContainsDiscriminatorPredicate(paren.Operand),
            SqlBetweenExpression between => ContainsDiscriminatorPredicate(between.Subject),
            SqlFunctionExpression func => func.Arguments.Any(ContainsDiscriminatorPredicate),
            SqlInExpression inExpr => ContainsDiscriminatorPredicate(inExpr.Item),
            DynamoScalarAccessExpression scalar => scalar.Parent is SqlExpression parentSql
                && ContainsDiscriminatorPredicate(parentSql),
            DynamoListIndexExpression listIdx => listIdx.Source is SqlExpression sourceSql
                && ContainsDiscriminatorPredicate(sourceSql),
            _ => false
        };

    /// <summary>
    ///     Recursively walks a SQL predicate expression and returns <c>true</c> if any
    ///     <see cref="SqlPropertyExpression" /> references an attribute outside the key set.
    /// </summary>
    private static bool ContainsNonKeyProperty(
        SqlExpression expression,
        HashSet<string> keyAttributes)
        => expression switch
        {
            SqlPropertyExpression prop => !keyAttributes.Contains(prop.PropertyName),
            SqlBinaryExpression bin => ContainsNonKeyProperty(bin.Left, keyAttributes)
                || ContainsNonKeyProperty(bin.Right, keyAttributes),
            SqlUnaryExpression unary => ContainsNonKeyProperty(unary.Operand, keyAttributes),
            SqlIsNullExpression isNull => ContainsNonKeyProperty(isNull.Operand, keyAttributes),
            // SqlParenthesizedExpression wraps a single inner expression via Operand.
            SqlParenthesizedExpression paren => ContainsNonKeyProperty(
                paren.Operand,
                keyAttributes),
            SqlBetweenExpression between => ContainsNonKeyProperty(between.Subject, keyAttributes),
            SqlFunctionExpression func => func.Arguments.Any(a
                => ContainsNonKeyProperty(a, keyAttributes)),
            // SqlInExpression: check both the item and inline values.
            SqlInExpression inExpr => ContainsNonKeyProperty(inExpr.Item, keyAttributes),
            // Constants and parameters are never property references.
            SqlConstantExpression or SqlParameterExpression => false,
            // Provider-injected discriminator predicates are handled by a dedicated safety check
            // in ValidateFirstTerminalSafePath. Keep them out of generic non-key classification
            // so key-only user predicates (e.g., PK+SK equality) are still evaluated correctly.
            SqlDiscriminatorPredicateExpression => false,
            // Nested path access (e.g. x.Profile.City → DynamoScalarAccessExpression): recurse
            // into the parent chain. The root of the chain is a SqlPropertyExpression whose name
            // identifies the top-level DynamoDB attribute or a
            // DynamoComplexPropertyAccessExpression
            // when the chain starts from a complex property. PK and SK are always scalar types and
            // can never host nested properties, so any nested path is inherently non-key.
            DynamoScalarAccessExpression scalar => scalar.Parent is not SqlExpression parentSql
                || ContainsNonKeyProperty(parentSql, keyAttributes),
            // Root complex-property access (e.g. x.Profile in x.Profile.City) is always non-key:
            // DynamoDB key attributes are scalar and cannot be complex documents.
            DynamoComplexPropertyAccessExpression => true,
            // List-index access (e.g. x.Tags[0] → DynamoListIndexExpression): recurse into the
            // source expression. PK and SK are never list types, so any list-index access is
            // inherently non-key.
            DynamoListIndexExpression listIdx => listIdx.Source is not SqlExpression sourceSql
                || ContainsNonKeyProperty(sourceSql, keyAttributes),
            _ => false
        };
}
