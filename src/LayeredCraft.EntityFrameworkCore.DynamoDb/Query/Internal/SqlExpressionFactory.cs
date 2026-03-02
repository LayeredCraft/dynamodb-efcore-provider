using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory for creating SQL expressions with proper type mappings.
/// </summary>
public class SqlExpressionFactory(ITypeMappingSource typeMappingSource) : ISqlExpressionFactory
{
    /// <inheritdoc />
    public SqlBinaryExpression Binary(
        ExpressionType operatorType,
        SqlExpression left,
        SqlExpression right)
    {
        // Determine the result type based on the operator
        var resultType = operatorType switch
        {
            ExpressionType.Equal
                or ExpressionType.NotEqual
                or ExpressionType.LessThan
                or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan
                or ExpressionType.GreaterThanOrEqual
                or ExpressionType.AndAlso
                or ExpressionType.OrElse => typeof(bool),
            _ => left.Type,
        };

        // Apply type mapping to operands if needed
        var typeMapping = left.TypeMapping ?? right.TypeMapping;
        if (typeMapping == null
            && operatorType is not (ExpressionType.AndAlso or ExpressionType.OrElse))
            typeMapping = typeMappingSource.FindMapping(left.Type);

        return new SqlBinaryExpression(operatorType, left, right, resultType, typeMapping);
    }

    /// <inheritdoc />
    public SqlConstantExpression Constant(object? value, Type? type = null)
    {
        type ??= value?.GetType() ?? typeof(object);
        var typeMapping = typeMappingSource.FindMapping(type);
        return new SqlConstantExpression(value, type, typeMapping);
    }

    /// <inheritdoc />
    public SqlParameterExpression Parameter(string name, Type type)
    {
        var typeMapping = typeMappingSource.FindMapping(type);
        return new SqlParameterExpression(name, type, typeMapping);
    }

    /// <inheritdoc />
    public SqlPropertyExpression Property(
        string propertyName,
        Type type,
        bool isPartitionKey = false)
    {
        var typeMapping = typeMappingSource.FindMapping(type);
        return new SqlPropertyExpression(propertyName, type, typeMapping, isPartitionKey);
    }

    /// <inheritdoc />
    public SqlInExpression In(
        SqlExpression item,
        IReadOnlyList<SqlExpression> values,
        bool isPartitionKeyComparison = false)
    {
        SqlInExpression.ValidateValueSource(values, null);
        var mappedItem = ApplyDefaultTypeMapping(item);
        return new SqlInExpression(mappedItem, values, null, isPartitionKeyComparison, null);
    }

    /// <inheritdoc />
    public SqlInExpression In(
        SqlExpression item,
        SqlParameterExpression valuesParameter,
        bool isPartitionKeyComparison = false)
    {
        SqlInExpression.ValidateValueSource(null, valuesParameter);
        var mappedItem = ApplyDefaultTypeMapping(item);
        return new SqlInExpression(
            mappedItem,
            null,
            valuesParameter,
            isPartitionKeyComparison,
            null);
    }

    /// <inheritdoc />
    public SqlFunctionExpression Function(
        string name,
        IReadOnlyList<SqlExpression> arguments,
        Type returnType)
        => new(name, arguments, returnType, typeMappingSource.FindMapping(returnType));

    /// <inheritdoc />
    public SqlUnaryExpression Not(SqlExpression operand) => new(ExpressionType.Not, operand);

    /// <inheritdoc />
    public SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, Type type)
    {
        var typeMapping = typeMappingSource.FindMapping(type);
        return sqlExpression switch
        {
            SqlConstantExpression constant => constant.ApplyTypeMapping(typeMapping),
            SqlParameterExpression parameter => parameter.ApplyTypeMapping(typeMapping),
            SqlPropertyExpression property => property.ApplyTypeMapping(typeMapping),
            _ => sqlExpression,
        };
    }

    /// <inheritdoc />
    public SqlExpression ApplyDefaultTypeMapping(SqlExpression sqlExpression)
    {
        if (sqlExpression.TypeMapping != null)
            return sqlExpression;

        return ApplyTypeMapping(sqlExpression, sqlExpression.Type);
    }
}
