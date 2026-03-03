using System.Collections;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Generates PartiQL SQL from a SelectExpression query model.
/// </summary>
public class DynamoQuerySqlGenerator : SqlExpressionVisitor
{
    private readonly StringBuilder _sql = new();
    private readonly List<AttributeValue> _parameters = [];
    private IReadOnlyDictionary<string, object?>? _parameterValues;

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
        }
    }

    /// <inheritdoc />
    protected override Expression VisitSelect(SelectExpression selectExpression)
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
        // Inline constant values directly into SQL using type mapping
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

    /// <summary>Emits <see cref="DynamoObjectAccessExpression" /> as a bare attribute name.</summary>
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

        var maxValues = sqlInExpression.IsPartitionKeyComparison ? 50 : 100;
        ValidateInValueCount(values.Count, maxValues, sqlInExpression.IsPartitionKeyComparison);

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

        var maxValues = sqlInExpression.IsPartitionKeyComparison ? 50 : 100;

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

            ValidateInValueCount(
                collection.Count,
                maxValues,
                sqlInExpression.IsPartitionKeyComparison);
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
                ValidateInValueCount(
                    count + 1,
                    maxValues,
                    sqlInExpression.IsPartitionKeyComparison);

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
        _parameters.Add(ConvertToAttributeValue(value, typeMapping));
    }

    /// <summary>Appends a predicate that is guaranteed to evaluate to false.</summary>
    private void AppendAlwaysFalsePredicate() => _sql.Append("1 = 0");

    /// <summary>
    /// Converts a CLR value to a DynamoDB AttributeValue.
    /// </summary>
    private static AttributeValue ConvertToAttributeValue(
        object? value,
        CoreTypeMapping? typeMapping)
    {
        // Apply value converter if present
        if (typeMapping?.Converter != null && value != null)
            value = typeMapping.Converter.ConvertToProvider(value);

        if (value == null)
            return new AttributeValue { NULL = true };

        return value switch
        {
            string s => new AttributeValue { S = s },
            bool b => new AttributeValue { BOOL = b },
            int i => new AttributeValue { N = i.ToString() },
            long l => new AttributeValue { N = l.ToString() },
            short sh => new AttributeValue { N = sh.ToString() },
            byte by => new AttributeValue { N = by.ToString() },
            double d => new AttributeValue { N = d.ToString("R") },
            float f => new AttributeValue { N = f.ToString("R") },
            decimal dec => new AttributeValue { N = dec.ToString() },
            Guid g => new AttributeValue { S = g.ToString() },
            DateTime dt => new AttributeValue { S = dt.ToString("O") },
            DateTimeOffset dto => new AttributeValue { S = dto.ToString("O") },
            _ => throw new NotSupportedException(
                $"Type {value.GetType()} is not supported for conversion to AttributeValue"),
        };
    }
}
