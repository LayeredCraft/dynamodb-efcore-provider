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
    private readonly List<(string Name, CoreTypeMapping? TypeMapping)> _parameterNames = [];

    /// <summary>
    /// Generates a PartiQL query from a SelectExpression.
    /// Tracks parameter names during SQL generation, then converts values to AttributeValues.
    /// </summary>
    public DynamoPartiQlQuery Generate(
        SelectExpression selectExpression,
        IReadOnlyDictionary<string, object?> parameterValues)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterNames.Clear();

        // Generate SQL with ? placeholders (doesn't need parameter values)
        VisitSelect(selectExpression);

        // After SQL generation, convert parameter values to AttributeValues
        foreach (var (name, typeMapping) in _parameterNames)
        {
            if (!parameterValues.TryGetValue(name, out var value))
                throw new InvalidOperationException(
                    $"Parameter '{name}' not found in parameter values.");

            var attributeValue = ConvertToAttributeValue(value, typeMapping);
            _parameters.Add(attributeValue);
        }

        return new DynamoPartiQlQuery(_sql.ToString(), _parameters);
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
        _sql.Append(selectExpression.TableName);

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
        // Track parameter name and type mapping (but don't look up value yet)
        _parameterNames.Add((sqlParameterExpression.Name, sqlParameterExpression.TypeMapping));

        // Write ? placeholder
        _sql.Append('?');

        return sqlParameterExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlProperty(SqlPropertyExpression sqlPropertyExpression)
    {
        // Quote property names if they contain special characters or are reserved words
        var propertyName = sqlPropertyExpression.PropertyName;
        if (NeedsQuoting(propertyName))
        {
            _sql.Append('"');
            _sql.Append(propertyName);
            _sql.Append('"');
        }
        else
        {
            _sql.Append(propertyName);
        }

        return sqlPropertyExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitProjection(ProjectionExpression projectionExpression)
    {
        // Visit the projected expression
        Visit(projectionExpression.Expression);
        return projectionExpression;
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

    private static bool NeedsQuoting(string identifier)
    {
        // Add more reserved words as needed
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT",
            "FROM",
            "WHERE",
            "ORDER",
            "BY",
            "ASC",
            "DESC",
            "AND",
            "OR",
            "NOT",
            "NULL",
            "TRUE",
            "FALSE",
        };

        return reservedWords.Contains(identifier) || identifier.Contains(' ');
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
