using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Generates PartiQL SQL from a SelectExpression query model.
/// </summary>
public class DynamoQuerySqlGenerator : SqlExpressionVisitor
{
    private readonly StringBuilder _sql = new();
    private readonly List<AttributeValue> _parameters = [];

    /// <summary>
    /// Generates a PartiQL query from a SelectExpression.
    /// </summary>
    /// <remarks>
    /// All SqlParameterExpression nodes should be inlined to SqlConstantExpression
    /// before calling this method using the ParameterInliner visitor.
    /// </remarks>
    public DynamoPartiQlQuery Generate(
        SelectExpression selectExpression,
        IReadOnlyDictionary<string, object?> parameterValues)
    {
        _sql.Clear();
        _parameters.Clear();

        VisitSelect(selectExpression);

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
        _sql.Append('(');
        Visit(sqlBinaryExpression.Left);
        _sql.Append(' ');
        _sql.Append(GetOperatorString(sqlBinaryExpression.OperatorType));
        _sql.Append(' ');
        Visit(sqlBinaryExpression.Right);
        _sql.Append(')');

        return sqlBinaryExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
    {
        // Always parameterize constant values
        var attributeValue = ConvertToAttributeValue(
            sqlConstantExpression.Value,
            sqlConstantExpression.TypeMapping);
        _parameters.Add(attributeValue);
        _sql.Append('?');

        return sqlConstantExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
        => throw
            // Parameters should have been inlined by ParameterInliner before SQL generation
            new InvalidOperationException(
                $"Encountered parameter '{sqlParameterExpression.Name}' during SQL generation. "
                + "All parameters should have been inlined before this point.");

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
