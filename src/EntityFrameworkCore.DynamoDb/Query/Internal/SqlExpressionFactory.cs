using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory for creating SQL expressions with proper type mappings.
/// </summary>
public sealed class SqlExpressionFactory(ITypeMappingSource typeMappingSource)
    : ISqlExpressionFactory
{
    /// <inheritdoc />
    public SqlBinaryExpression Binary(
        ExpressionType operatorType,
        SqlExpression left,
        SqlExpression right)
    {
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

        var isLogical = operatorType is ExpressionType.AndAlso or ExpressionType.OrElse;
        var operandTypeMapping =
            isLogical ? typeMappingSource.FindMapping(typeof(bool)) : InferTypeMapping(left, right);
        if (operandTypeMapping == null && !isLogical)
            operandTypeMapping = typeMappingSource.FindMapping(left.Type);

        if (operandTypeMapping != null)
        {
            left = ApplyTypeMapping(left, operandTypeMapping);
            right = ApplyTypeMapping(right, operandTypeMapping);
        }

        var resultTypeMapping = resultType == typeof(bool)
            ? typeMappingSource.FindMapping(typeof(bool))
            : operandTypeMapping;

        return new SqlBinaryExpression(operatorType, left, right, resultType, resultTypeMapping);
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
        var itemTypeMapping = mappedItem.TypeMapping;
        var mappedValues = itemTypeMapping == null
            ? values
            : values.Select(value => ApplyTypeMapping(value, itemTypeMapping)).ToArray();

        return new SqlInExpression(
            mappedItem,
            mappedValues,
            null,
            isPartitionKeyComparison,
            typeMappingSource.FindMapping(typeof(bool)));
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
            typeMappingSource.FindMapping(typeof(bool)));
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
    public SqlIsNullExpression IsNull(SqlExpression operand) => new(operand, IsNullOperator.IsNull);

    /// <inheritdoc />
    public SqlIsNullExpression IsNotNull(SqlExpression operand)
        => new(operand, IsNullOperator.IsNotNull);

    /// <inheritdoc />
    public SqlIsNullExpression IsMissing(SqlExpression operand)
        => new(operand, IsNullOperator.IsMissing);

    /// <inheritdoc />
    public SqlIsNullExpression IsNotMissing(SqlExpression operand)
        => new(operand, IsNullOperator.IsNotMissing);

    /// <inheritdoc />
    public SqlBetweenExpression Between(
        SqlExpression subject,
        SqlExpression low,
        SqlExpression high)
    {
        var typeMapping = InferTypeMapping(subject, low, high)
            ?? typeMappingSource.FindMapping(subject.Type);

        if (typeMapping != null)
        {
            subject = ApplyTypeMapping(subject, typeMapping);
            low = ApplyTypeMapping(low, typeMapping);
            high = ApplyTypeMapping(high, typeMapping);
        }

        return new SqlBetweenExpression(subject, low, high);
    }

    private static CoreTypeMapping? InferTypeMapping(params SqlExpression[] expressions)
    {
        foreach (var expression in expressions)
            if (expression is not SqlConstantExpression and not SqlParameterExpression
                && expression.TypeMapping is { } typeMapping)
                return typeMapping;

        foreach (var expression in expressions)
            if (expression.TypeMapping is { } typeMapping)
                return typeMapping;

        return null;
    }

    /// <inheritdoc />
    public SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, Type type)
        => ApplyTypeMapping(sqlExpression, typeMappingSource.FindMapping(type));

    /// <inheritdoc />
    public SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, CoreTypeMapping? typeMapping)
        => sqlExpression switch
        {
            SqlConstantExpression constant => constant.ApplyTypeMapping(typeMapping),
            SqlParameterExpression parameter => parameter.ApplyTypeMapping(typeMapping),
            SqlPropertyExpression property => property.ApplyTypeMapping(typeMapping),
            DynamoScalarAccessExpression scalarAccess => scalarAccess.ApplyTypeMapping(typeMapping),
            _ => sqlExpression,
        };

    /// <inheritdoc />
    public SqlExpression ApplyDefaultTypeMapping(SqlExpression sqlExpression)
    {
        if (sqlExpression.TypeMapping != null)
            return sqlExpression;

        return ApplyTypeMapping(sqlExpression, sqlExpression.Type);
    }
}
