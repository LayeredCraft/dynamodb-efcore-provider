using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Removes ProjectionBindingExpression nodes and replaces them with concrete expressions
/// that access Dictionary&lt;string, AttributeValue&gt; to extract property values.
/// This is the critical bridge between EF Core's abstract query model and DynamoDB's data format.
/// </summary>
public class DynamoProjectionBindingRemovingExpressionVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _itemParameter;

    public DynamoProjectionBindingRemovingExpressionVisitor(ParameterExpression itemParameter)
        => _itemParameter = itemParameter;

    protected override Expression VisitNew(NewExpression node)
    {
        // Intercept MaterializationContext constructor and pass ValueBuffer.Empty as placeholder
        // The actual data will come from Dictionary<string, AttributeValue> via intercepted reads
        if (node.Arguments.Count > 0 && node.Arguments[0] is ProjectionBindingExpression)
        {
            // Replace ProjectionBindingExpression with ValueBuffer.Empty
            // This satisfies the constructor signature without actually using the buffer
            var newArguments = new List<Expression>(node.Arguments.Count);
            newArguments.Add(Expression.Constant(ValueBuffer.Empty));

            // Visit the remaining arguments
            for (var i = 1; i < node.Arguments.Count; i++)
                newArguments.Add(Visit(node.Arguments[i]));

            return node.Update(newArguments);
        }

        return base.VisitNew(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        // Intercept ValueBufferTryReadValue calls created by InjectStructuralTypeMaterializers
        // and replace them with Dictionary<string, AttributeValue> property access
        if (methodCallExpression.Method.IsGenericMethod)
        {
            var genericMethod = methodCallExpression.Method.GetGenericMethodDefinition();

            if (genericMethod == ExpressionExtensions.ValueBufferTryReadValueMethod)
            {
                // ValueBufferTryReadValue<T>(valueBuffer, index, property)
                // Replace with: GetValue<T>(dictionary, property)

                var property =
                    (IProperty)((ConstantExpression)methodCallExpression.Arguments[2]).Value!;

                // Use property.ClrType as the target type since methodCallExpression.Type might be
                // 'object'
                // This ensures we deserialize to the correct type before any conversions
                var targetType = methodCallExpression.Type;

                // If the method returns object, use the property's actual CLR type
                if (targetType == typeof(object))
                {
                    targetType = property.ClrType;
                }

                // Replace ValueBuffer.TryReadValue<T>(...) with GetValue<T>(dictionary, property)
                // Always use _itemParameter (our Dictionary<string, AttributeValue>)
                var getValueCall = Expression.Call(
                    GetValueMethod.MakeGenericMethod(targetType),
                    _itemParameter,
                    Expression.Constant(property, typeof(IReadOnlyProperty)));

                // If the original method expected a different type (e.g., object), add a conversion
                if (getValueCall.Type != methodCallExpression.Type)
                {
                    return Expression.Convert(getValueCall, methodCallExpression.Type);
                }

                return getValueCall;
            }
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    private static readonly MethodInfo GetValueMethod =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(GetValue),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Helper method to extract a value from Dictionary&lt;string, AttributeValue&gt;
    /// using EF Core's type mapping and value converter system
    /// </summary>
    internal static T? GetValue<T>(
        Dictionary<string, AttributeValue> item,
        IReadOnlyProperty property)
    {
        var propertyName = property.Name;

        if (!item.TryGetValue(propertyName, out var attributeValue))
            return default;

        if (attributeValue.NULL == true)
            return default;

        // Convert from DynamoDB AttributeValue to store/provider type
        var storeValue = ConvertAttributeValueToStoreType(attributeValue, property.ClrType);

        if (storeValue == null)
            return default;

        // Apply value converter if one exists
        var typeMapping = property.GetTypeMapping();
        if (typeMapping.Converter != null)
        {
            var convertedValue = typeMapping.Converter.ConvertFromProvider(storeValue);
            return (T)convertedValue!;
        }

        return (T)storeValue;
    }

    /// <summary>
    /// Converts DynamoDB AttributeValue to the appropriate CLR type
    /// </summary>
    private static object? ConvertAttributeValueToStoreType(
        AttributeValue attributeValue,
        Type clrType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        // String type
        if (nonNullableType == typeof(string))
            return attributeValue.S;

        // Boolean type
        if (nonNullableType == typeof(bool))
            return attributeValue.BOOL;

        // Numeric types - DynamoDB stores all numbers as strings in the N property
        if (!string.IsNullOrEmpty(attributeValue.N))
        {
            if (nonNullableType == typeof(int))
                return int.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(long))
                return long.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(short))
                return short.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(byte))
                return byte.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(double))
                return double.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(float))
                return float.Parse(attributeValue.N, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(decimal))
                return decimal.Parse(attributeValue.N, CultureInfo.InvariantCulture);
        }

        // Guid - typically stored as string
        if (nonNullableType == typeof(Guid) && !string.IsNullOrEmpty(attributeValue.S))
            return Guid.Parse(attributeValue.S);

        // DateTime - typically stored as string ISO8601 or as number (Unix timestamp)
        if (nonNullableType == typeof(DateTime))
        {
            if (!string.IsNullOrEmpty(attributeValue.S))
                return DateTime.Parse(
                    attributeValue.S,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

            if (!string.IsNullOrEmpty(attributeValue.N))
                return DateTimeOffset
                    .FromUnixTimeSeconds(long.Parse(attributeValue.N, CultureInfo.InvariantCulture))
                    .DateTime;
        }

        // DateTimeOffset
        if (nonNullableType == typeof(DateTimeOffset))
        {
            if (!string.IsNullOrEmpty(attributeValue.S))
                return DateTimeOffset.Parse(
                    attributeValue.S,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

            if (!string.IsNullOrEmpty(attributeValue.N))
                return DateTimeOffset.FromUnixTimeSeconds(
                    long.Parse(attributeValue.N, CultureInfo.InvariantCulture));
        }

        return null;
    }
}
