using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
                    targetType = property.ClrType;

                // Replace ValueBuffer.TryReadValue<T>(...) with GetValue<T>(dictionary, property)
                // Always use _itemParameter (our Dictionary<string, AttributeValue>)
                var getValueCall = Expression.Call(
                    GetValueMethod.MakeGenericMethod(targetType),
                    _itemParameter,
                    Expression.Constant(property, typeof(IReadOnlyProperty)));

                // If the original method expected a different type (e.g., object), add a conversion
                if (getValueCall.Type != methodCallExpression.Type)
                    return Expression.Convert(getValueCall, methodCallExpression.Type);

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
    /// using EF Core's type mapping and value converter system.
    ///
    /// <para>
    /// This method leverages the registered AttributeValue-based converters from
    /// <see cref="DynamoTypeMappingSource"/> to perform type conversion. All conversion
    /// logic is centralized in the converter classes, eliminating the need for manual
    /// type checking.
    /// </para>
    ///
    /// <para>
    /// To customize parsing formats (e.g., DateTime formats, culture info), users can
    /// provide their own ValueConverter when configuring entities:
    /// <code>
    /// modelBuilder.Entity&lt;MyEntity&gt;()
    ///     .Property(e => e.CreatedAt)
    ///     .HasConversion(new ValueConverter&lt;DateTime, AttributeValue&gt;(
    ///         dt => new AttributeValue { S = dt.ToString("your-format") },
    ///         av => DateTime.ParseExact(av.S, "your-format", yourCulture)));
    /// </code>
    /// </para>
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

        // Get the type mapping which contains the AttributeValue converter
        var typeMapping = property.GetTypeMapping();
        var converter = typeMapping.Converter;

        if (converter == null)
            throw new InvalidOperationException(
                $"No value converter registered for property '{property.Name}' of type '{typeof(T)}'. "
                + "Ensure that DynamoTypeMappingSource has registered a converter for this type.");

        if (converter is ValueConverter<T, AttributeValue> typedConverter)
            return typedConverter.ConvertFromProviderTyped(attributeValue); // NO BOXING!

        // Use the converter to transform AttributeValue → CLR type
        // The converter handles all parsing logic (culture info, date formats, etc.)
        var convertedValue = converter.ConvertFromProvider(attributeValue);
        return (T)convertedValue!;
    }
}
