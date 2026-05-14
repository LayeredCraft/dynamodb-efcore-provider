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
        // A string-converted enum compared to its underlying numeric value would bind the value
        // against an S attribute. Reject that shape instead of silently producing no matches.
        if (!isLogical)
            ThrowIfConvertedEnumComparedToUnderlyingNumber(left, right);

        var operandTypeMapping = isLogical
            ? typeMappingSource.FindMapping(typeof(bool))
            : InferTypeMapping(left, right);
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
        var mappedValues = values;
        if (itemTypeMapping != null)
        {
            var remappedValues = new SqlExpression[values.Count];
            var changed = false;
            for (var i = 0; i < values.Count; i++)
            {
                ThrowIfConvertedEnumComparedToUnderlyingNumber(mappedItem, values[i]);
                remappedValues[i] = ApplyTypeMapping(values[i], itemTypeMapping);
                changed |= !ReferenceEquals(remappedValues[i], values[i]);
            }

            if (changed)
                mappedValues = remappedValues;
        }

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
        ThrowIfConvertedEnumComparedToUnderlyingNumber(mappedItem, valuesParameter);
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
            ThrowIfConvertedEnumComparedToUnderlyingNumber(subject, low);
            ThrowIfConvertedEnumComparedToUnderlyingNumber(subject, high);
            subject = ApplyTypeMapping(subject, typeMapping);
            low = ApplyTypeMapping(low, typeMapping);
            high = ApplyTypeMapping(high, typeMapping);
        }

        return new SqlBetweenExpression(subject, low, high);
    }

    private static void ThrowIfConvertedEnumComparedToUnderlyingNumber(
        SqlExpression left,
        SqlExpression right)
    {
        if (IsConvertedEnumComparedToUnderlyingNumberFrom(left, right)
            || IsConvertedEnumComparedToUnderlyingNumberFrom(right, left))
            throw new InvalidOperationException(
                DynamoStrings.ConvertedEnumUnderlyingCastNotSupported);
    }

    private static bool IsConvertedEnumComparedToUnderlyingNumberFrom(
        SqlExpression enumExpression,
        SqlExpression otherExpression)
    {
        var enumType = Nullable.GetUnderlyingType(enumExpression.Type) ?? enumExpression.Type;
        if (!enumType.IsEnum || enumExpression.TypeMapping?.Converter == null)
            return false;

        return otherExpression is SqlParameterExpression
            && GetValueOrElementType(otherExpression.Type) == Enum.GetUnderlyingType(enumType);
    }

    private static Type GetValueOrElementType(Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (nonNullableType.IsArray)
            return nonNullableType.GetElementType()!;

        return nonNullableType.IsGenericType
            && nonNullableType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? nonNullableType.GetGenericArguments()[0]
                : nonNullableType;
    }

    private static CoreTypeMapping? InferTypeMapping(SqlExpression first, SqlExpression second)
        => PreferNonValueMapping(first)
            ?? PreferNonValueMapping(second) ?? first.TypeMapping ?? second.TypeMapping;

    private static CoreTypeMapping? InferTypeMapping(
        SqlExpression first,
        SqlExpression second,
        SqlExpression third)
        => PreferNonValueMapping(first)
            ?? PreferNonValueMapping(second)
            ?? PreferNonValueMapping(third)
            ?? first.TypeMapping ?? second.TypeMapping ?? third.TypeMapping;

    private static CoreTypeMapping? PreferNonValueMapping(SqlExpression expression)
        => expression is not SqlConstantExpression and not SqlParameterExpression
            ? expression.TypeMapping
            : null;

    /// <inheritdoc />
    public SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, Type type)
        => ApplyTypeMapping(sqlExpression, typeMappingSource.FindMapping(type));

    /// <inheritdoc />
    public SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, CoreTypeMapping? typeMapping)
    {
        if (ReferenceEquals(sqlExpression.TypeMapping, typeMapping))
            return sqlExpression;

        return sqlExpression switch
        {
            SqlConstantExpression constant => constant.ApplyTypeMapping(typeMapping),
            SqlParameterExpression parameter => parameter.ApplyTypeMapping(typeMapping),
            SqlPropertyExpression property => property.ApplyTypeMapping(typeMapping),
            DynamoScalarAccessExpression scalarAccess => scalarAccess.ApplyTypeMapping(typeMapping),
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
