using System.Collections;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Generates PartiQL SQL from a SelectExpression query model.
/// </summary>
public class DynamoQuerySqlGenerator : SqlExpressionVisitor
{
    private readonly StringBuilder _sql = new();
    private readonly List<AttributeValue> _parameters = [];
    private IReadOnlyDictionary<string, object?>? _parameterValues;
    private SelectExpression? _currentSelectExpression;

    /// <summary>
    /// Generates a PartiQL query from a SelectExpression.
    /// Uses runtime parameter values during SQL generation to support dynamic IN expansion.
    /// </summary>
    public DynamoPartiQlQuery Generate(
        SelectExpression selectExpression,
        IReadOnlyDictionary<string, object?> parameterValues)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterValues = parameterValues;

        try
        {
            VisitSelect(selectExpression);
            return new DynamoPartiQlQuery(_sql.ToString(), _parameters);
        }
        finally
        {
            _parameterValues = null;
            _currentSelectExpression = null;
        }
    }

    /// <inheritdoc />
    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        var previousSelectExpression = _currentSelectExpression;
        _currentSelectExpression = selectExpression;

        try
        {
            _sql.Append("SELECT ");

            // Projections are MANDATORY - no SELECT * support
            if (selectExpression.Projection.Count == 0)
                throw new InvalidOperationException(
                    "SelectExpression must have at least one projection. SELECT * is not supported.");

            // Generate column list
            for (var i = 0; i < selectExpression.Projection.Count; i++)
            {
                if (i > 0)
                    _sql.Append(", ");
                Visit(selectExpression.Projection[i].Expression);
            }

            _sql.Append("\nFROM ");
            AppendIdentifier(selectExpression.TableName);
            if (selectExpression.IndexName is { } indexName)
            {
                _sql.Append('.');
                AppendIdentifier(indexName);
            }

            if (selectExpression.Predicate != null)
            {
                _sql.Append("\nWHERE ");
                Visit(selectExpression.Predicate);
            }

            if (selectExpression.Orderings.Count > 0)
            {
                _sql.Append("\nORDER BY ");
                for (var i = 0; i < selectExpression.Orderings.Count; i++)
                {
                    if (i > 0)
                        _sql.Append(", ");
                    VisitOrdering(selectExpression.Orderings[i]);
                }
            }

            return selectExpression;
        }
        finally
        {
            _currentSelectExpression = previousSelectExpression;
        }
    }

    /// <summary>
    /// Visits an ordering expression.
    /// </summary>
    protected virtual void VisitOrdering(OrderingExpression orderingExpression)
    {
        Visit(orderingExpression.Expression);
        _sql.Append(orderingExpression.IsAscending ? " ASC" : " DESC");
    }

    /// <inheritdoc />
    protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
    {
        // Check if left operand needs parentheses
        var leftRequiresParentheses = RequiresParentheses(
            sqlBinaryExpression,
            sqlBinaryExpression.Left);

        if (leftRequiresParentheses)
            _sql.Append('(');

        Visit(sqlBinaryExpression.Left);

        if (leftRequiresParentheses)
            _sql.Append(')');

        _sql.Append(' ');
        _sql.Append(GetOperatorString(sqlBinaryExpression.OperatorType));
        _sql.Append(' ');

        // Check if right operand needs parentheses
        var rightRequiresParentheses = RequiresParentheses(
            sqlBinaryExpression,
            sqlBinaryExpression.Right);

        if (rightRequiresParentheses)
            _sql.Append('(');

        Visit(sqlBinaryExpression.Right);

        if (rightRequiresParentheses)
            _sql.Append(')');

        return sqlBinaryExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlParenthesized(
        SqlParenthesizedExpression sqlParenthesizedExpression)
    {
        _sql.Append('(');
        Visit(sqlParenthesizedExpression.Operand);
        _sql.Append(')');

        return sqlParenthesizedExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlUnary(SqlUnaryExpression sqlUnaryExpression)
    {
        _sql.Append("NOT (");
        Visit(sqlUnaryExpression.Operand);
        _sql.Append(')');

        return sqlUnaryExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
    {
        // Constants and parameters must go through the same mapping-owned serialization rules so
        // inline literals and AttributeValue parameters stay behaviorally aligned.
        if (sqlConstantExpression.TypeMapping is DynamoTypeMapping dynamoTypeMapping)
            _sql.Append(dynamoTypeMapping.GenerateConstant(sqlConstantExpression.Value));
        else
            // Fallback for cases without type mapping (shouldn't happen in normal usage)
            throw new InvalidOperationException(
                $"SqlConstantExpression requires a DynamoTypeMapping. Got: {sqlConstantExpression.TypeMapping?.GetType().Name ?? "null"}");

        return sqlConstantExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
    {
        if (_parameterValues == null)
            throw new InvalidOperationException(
                "Parameter values are unavailable during SQL generation.");

        if (!_parameterValues.TryGetValue(sqlParameterExpression.Name, out var value))
            throw new InvalidOperationException(
                $"Parameter '{sqlParameterExpression.Name}' not found in parameter values.");

        AppendParameter(value, sqlParameterExpression.TypeMapping);

        return sqlParameterExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlIn(SqlInExpression sqlInExpression)
    {
        if (sqlInExpression.Values != null)
            return VisitInlineIn(sqlInExpression);

        return VisitParameterizedIn(sqlInExpression);
    }

    /// <inheritdoc />
    protected override Expression VisitSqlIsNull(SqlIsNullExpression sqlIsNullExpression)
    {
        Visit(sqlIsNullExpression.Operand);
        _sql.Append(
            sqlIsNullExpression.Operator switch
            {
                IsNullOperator.IsNull => " IS NULL",
                IsNullOperator.IsNotNull => " IS NOT NULL",
                IsNullOperator.IsMissing => " IS MISSING",
                IsNullOperator.IsNotMissing => " IS NOT MISSING",
                _ => throw new NotSupportedException(
                    $"IS operator '{sqlIsNullExpression.Operator}' is not supported."),
            });
        return sqlIsNullExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlBetween(SqlBetweenExpression sqlBetweenExpression)
    {
        Visit(sqlBetweenExpression.Subject);
        _sql.Append(" BETWEEN ");
        Visit(sqlBetweenExpression.Low);
        _sql.Append(" AND ");
        Visit(sqlBetweenExpression.High);
        return sqlBetweenExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
    {
        _sql.Append(sqlFunctionExpression.Name);
        _sql.Append('(');

        for (var i = 0; i < sqlFunctionExpression.Arguments.Count; i++)
        {
            if (i > 0)
                _sql.Append(", ");

            Visit(sqlFunctionExpression.Arguments[i]);
        }

        _sql.Append(')');
        return sqlFunctionExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlProperty(SqlPropertyExpression sqlPropertyExpression)
    {
        AppendIdentifier(sqlPropertyExpression.PropertyName);

        return sqlPropertyExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitProjection(ProjectionExpression projectionExpression)
    {
        // Visit the projected expression
        Visit(projectionExpression.Expression);
        return projectionExpression;
    }

    /// <summary>Emits <c>DynamoObjectAccessExpression</c> as a bare attribute name.</summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is DynamoObjectAccessExpression objectAccess)
        {
            AppendIdentifier(objectAccess.PropertyName);

            return objectAccess;
        }

        return base.VisitExtension(node);
    }

    /// <summary>
    ///     Emits a nested scalar path segment as <c>"Parent"."PropertyName"</c> by recursively
    ///     visiting the parent first then appending a dot and the quoted segment name.
    /// </summary>
    protected override Expression VisitDynamoScalarAccess(
        DynamoScalarAccessExpression scalarAccessExpression)
    {
        Visit(scalarAccessExpression.Parent);
        _sql.Append('.');
        AppendIdentifier(scalarAccessExpression.PropertyName);
        return scalarAccessExpression;
    }

    /// <summary>
    ///     Emits a list element access as <c>"Source"[index]</c> by visiting the source expression
    ///     then appending the literal integer index in brackets.
    /// </summary>
    protected override Expression VisitDynamoListIndex(
        DynamoListIndexExpression listIndexExpression)
    {
        Visit(listIndexExpression.Source);
        _sql.Append('[');
        _sql.Append(listIndexExpression.Index);
        _sql.Append(']');
        return listIndexExpression;
    }

    /// <summary>
    ///     Unwraps the discriminator wrapper and emits the inner predicate as plain SQL. The wrapper
    ///     is a provider implementation detail; the emitted PartiQL is identical to what the inner
    ///     expression would produce on its own.
    /// </summary>
    protected override Expression VisitSqlDiscriminatorPredicate(
        SqlDiscriminatorPredicateExpression discriminatorPredicateExpression)
    {
        Visit(discriminatorPredicateExpression.Predicate);
        return discriminatorPredicateExpression;
    }

    /// <summary>
    /// Gets the precedence and associativity for an operator expression.
    /// Based on standard SQL operator precedence.
    /// </summary>
    protected virtual bool TryGetOperatorInfo(
        SqlExpression expression,
        out int precedence,
        out bool isAssociative)
    {
        (precedence, isAssociative) = expression switch
        {
            SqlBinaryExpression binary => binary.OperatorType switch
            {
                ExpressionType.Multiply => (900, true),
                ExpressionType.Divide => (900, false),
                ExpressionType.Add => (700, true),
                ExpressionType.Subtract => (700, false),
                ExpressionType.Equal => (500, false),
                ExpressionType.NotEqual => (500, false),
                ExpressionType.LessThan => (500, false),
                ExpressionType.LessThanOrEqual => (500, false),
                ExpressionType.GreaterThan => (500, false),
                ExpressionType.GreaterThanOrEqual => (500, false),
                ExpressionType.AndAlso => (200, true),
                ExpressionType.OrElse => (100, true),
                _ => default,
            },
            _ => default,
        };

        return precedence != default;
    }

    /// <summary>
    /// Determines if an inner expression needs parentheses when nested inside an outer expression.
    /// </summary>
    protected virtual bool RequiresParentheses(
        SqlExpression outerExpression,
        SqlExpression innerExpression)
    {
        // Non-binary expressions never need parentheses
        if (innerExpression is not SqlBinaryExpression)
            return false;

        // If outer is not binary, no parentheses needed
        if (outerExpression is not SqlBinaryExpression outerBinary)
            return false;

        // Get precedence info for both expressions
        if (!TryGetOperatorInfo(outerExpression, out var outerPrecedence, out _))
            return true; // Conservative: add parentheses if precedence unknown

        if (!TryGetOperatorInfo(
            innerExpression,
            out var innerPrecedence,
            out var innerIsAssociative))
            return true; // Conservative: add parentheses if precedence unknown

        var innerBinary = (SqlBinaryExpression)innerExpression;

        // If outer has higher precedence, inner needs parentheses
        if (outerPrecedence > innerPrecedence)
            return true;

        // If outer has lower precedence, no parentheses needed
        if (outerPrecedence < innerPrecedence)
            return false;

        // Same precedence: check if same operator and associative
        if (outerBinary.OperatorType == innerBinary.OperatorType && innerIsAssociative)
            return false;

        // Special case: AND inside OR should have parentheses for readability
        // (a OR b) AND c doesn't need inner parens (AND is tighter)
        // a AND (b OR c) needs inner parens (OR is looser)
        if (outerBinary.OperatorType == ExpressionType.AndAlso
            && innerBinary.OperatorType == ExpressionType.OrElse)
            return true;

        // Same precedence, different operators: needs parentheses
        return true;
    }

    /// <summary>Appends a quoted identifier to the SQL buffer, escaping embedded double-quotes.</summary>
    private void AppendIdentifier(string identifier)
    {
        _sql.Append('"');
        _sql.Append(identifier.Replace("\"", "\"\"", StringComparison.Ordinal));
        _sql.Append('"');
    }

    private static string GetOperatorString(ExpressionType operatorType)
        => operatorType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => throw new NotSupportedException($"Operator type {operatorType} is not supported"),
        };

    /// <summary>Emits an IN predicate using inline SQL values.</summary>
    private Expression VisitInlineIn(SqlInExpression sqlInExpression)
    {
        var values = sqlInExpression.Values!;
        if (values.Count == 0)
        {
            AppendAlwaysFalsePredicate();
            return sqlInExpression;
        }

        var isPartitionKeyComparison = IsPartitionKeyComparison(sqlInExpression);
        var maxValues = isPartitionKeyComparison ? 50 : 100;
        ValidateInValueCount(values.Count, maxValues, isPartitionKeyComparison);

        Visit(sqlInExpression.Item);
        _sql.Append(" IN [");

        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                _sql.Append(", ");

            Visit(values[i]);
        }

        _sql.Append(']');
        return sqlInExpression;
    }

    /// <summary>Emits an IN predicate using runtime parameter expansion.</summary>
    private Expression VisitParameterizedIn(SqlInExpression sqlInExpression)
    {
        if (_parameterValues == null)
            throw new InvalidOperationException(
                "Parameter values are unavailable during SQL generation.");

        var valuesParameter = sqlInExpression.ValuesParameter
            ?? throw new InvalidOperationException("IN expression parameter values are required.");

        if (!_parameterValues.TryGetValue(valuesParameter.Name, out var parameterValue))
            throw new InvalidOperationException(
                $"Parameter '{valuesParameter.Name}' not found in parameter values.");

        var isPartitionKeyComparison = IsPartitionKeyComparison(sqlInExpression);
        var maxValues = isPartitionKeyComparison ? 50 : 100;

        if (parameterValue is null)
        {
            AppendAlwaysFalsePredicate();
            return sqlInExpression;
        }

        if (parameterValue is string || parameterValue is not IEnumerable enumerable)
            throw new InvalidOperationException(
                DynamoStrings.ContainsCollectionParameterMustBeEnumerable);

        var enforceLimitDuringEnumeration = true;
        if (parameterValue is ICollection collection)
        {
            if (collection.Count == 0)
            {
                AppendAlwaysFalsePredicate();
                return sqlInExpression;
            }

            ValidateInValueCount(collection.Count, maxValues, isPartitionKeyComparison);
            enforceLimitDuringEnumeration = false;
        }

        var sqlLengthBeforePredicate = _sql.Length;
        var parameterCountBeforePredicate = _parameters.Count;

        Visit(sqlInExpression.Item);
        _sql.Append(" IN [");

        var count = 0;

        foreach (var value in enumerable)
        {
            if (enforceLimitDuringEnumeration)
                ValidateInValueCount(count + 1, maxValues, isPartitionKeyComparison);

            if (count > 0)
                _sql.Append(", ");

            AppendParameter(value, sqlInExpression.Item.TypeMapping);
            count++;
        }

        if (count == 0)
        {
            _sql.Length = sqlLengthBeforePredicate;

            if (_parameters.Count > parameterCountBeforePredicate)
                _parameters.RemoveRange(
                    parameterCountBeforePredicate,
                    _parameters.Count - parameterCountBeforePredicate);

            AppendAlwaysFalsePredicate();
            return sqlInExpression;
        }

        _sql.Append(']');
        return sqlInExpression;
    }

    /// <summary>
    ///     Determines whether the IN comparison targets an effective partition key for the finalized
    ///     query source.
    /// </summary>
    /// <returns>
    ///     <c>true</c> when the IN item is a table or selected-index partition key; otherwise
    ///     <c>false</c>.
    /// </returns>
    private bool IsPartitionKeyComparison(SqlInExpression sqlInExpression)
    {
        if (_currentSelectExpression is not { } selectExpression)
            return sqlInExpression.IsPartitionKeyComparison;

        if (selectExpression.EffectivePartitionKeyPropertyNames.Count == 0)
            return sqlInExpression.IsPartitionKeyComparison;

        return TryGetRootPropertyName(sqlInExpression.Item) is { } propertyName
            && selectExpression.EffectivePartitionKeyPropertyNames.Contains(propertyName);
    }

    /// <summary>Tries to resolve the root property name for an IN-item expression.</summary>
    /// <returns>The root property name when resolvable; otherwise <c>null</c>.</returns>
    private static string? TryGetRootPropertyName(SqlExpression expression)
        => expression switch
        {
            SqlPropertyExpression propertyExpression => propertyExpression.PropertyName,
            SqlParenthesizedExpression parenthesizedExpression => TryGetRootPropertyName(
                parenthesizedExpression.Operand),
            DynamoScalarAccessExpression scalarAccessExpression =>
                scalarAccessExpression.Parent is SqlExpression parentSqlExpression
                    ? TryGetRootPropertyName(parentSqlExpression)
                    : null,
            _ => null,
        };

    /// <summary>Validates IN-list value count against DynamoDB limits.</summary>
    private static void ValidateInValueCount(
        int count,
        int maxValues,
        bool isPartitionKeyComparison)
    {
        if (count > maxValues)
            // TODO: Consider supporting chunked IN execution or BatchGetItem optimization.
            throw new InvalidOperationException(
                DynamoStrings.InListTooLarge(maxValues, isPartitionKeyComparison));
    }

    /// <summary>Appends a parameter placeholder and converted AttributeValue in lockstep.</summary>
    private void AppendParameter(object? value, CoreTypeMapping? typeMapping)
    {
        _sql.Append('?');
        _parameters.Add(
            // Parameter conversion is model-value-facing; the mapping composes any EF converter and
            // then serializes to the correct DynamoDB wire shape.
            (typeMapping as DynamoTypeMapping)?.CreateAttributeValue(value)
            ?? throw new InvalidOperationException(
                $"Query parameter requires a DynamoTypeMapping. Got: {typeMapping?.GetType().Name ?? "null"}"));
    }

    /// <summary>Appends a predicate that is guaranteed to evaluate to false.</summary>
    private void AppendAlwaysFalsePredicate() => _sql.Append("1 = 0");
}
