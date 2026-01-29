using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Removes ProjectionBindingExpression nodes and replaces them with concrete expressions
/// that access Dictionary&lt;string, AttributeValue&gt; to extract property values.
///
/// <para>
/// This visitor builds expression trees at query compilation time (not runtime) to eliminate
/// boxing/unboxing of value types. It inlines EF Core converter expression trees directly
/// into the final compiled query, following the same pattern as the Cosmos provider.
/// </para>
///
/// <para>
/// Performance: Expression trees are compiled once per query shape, then executed many times.
/// By inlining converters, we avoid runtime method calls and boxing operations.
/// </para>
/// </summary>
public class DynamoProjectionBindingRemovingExpressionVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _itemParameter;

    // Reflection cache for AttributeValue properties
    private static readonly PropertyInfo AttributeValueSProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.S))!;

    private static readonly PropertyInfo AttributeValueBoolProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.BOOL))!;

    private static readonly PropertyInfo AttributeValueNProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.N))!;

    private static readonly PropertyInfo AttributeValueBProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.B))!;

    private static readonly PropertyInfo AttributeValueNullProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.NULL))!;

    private static readonly MethodInfo DictionaryTryGetValueMethod =
        typeof(Dictionary<string, AttributeValue>).GetMethod(
            nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

    private static readonly MethodInfo LongParseMethod =
        typeof(long).GetMethod(nameof(long.Parse), [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo DoubleParseMethod =
        typeof(double).GetMethod(nameof(double.Parse), [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo DecimalParseMethod =
        typeof(decimal).GetMethod(
            nameof(decimal.Parse),
            [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo MemoryStreamToArrayMethod =
        typeof(MemoryStream).GetMethod(nameof(MemoryStream.ToArray))!;

    private static readonly MethodInfo StringIsNullOrEmptyMethod =
        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;

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
            List<Expression> newArguments = [Constant(ValueBuffer.Empty)];

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
        // and replace them with inline expression trees for Dictionary<string, AttributeValue>
        // access
        if (methodCallExpression.Method.IsGenericMethod)
        {
            var genericMethod = methodCallExpression.Method.GetGenericMethodDefinition();

            if (genericMethod == ExpressionExtensions.ValueBufferTryReadValueMethod)
            {
                // ValueBufferTryReadValue<T>(valueBuffer, index, property)
                // Replace with inline expression tree: no runtime method call, no boxing!

                var property =
                    (IProperty)((ConstantExpression)methodCallExpression.Arguments[2]).Value!;

                var targetType = methodCallExpression.Type;

                // If the method returns object, use the property's actual CLR type
                if (targetType == typeof(object))
                    targetType = property.ClrType;

                // Build expression tree for value extraction (compiled once, executed many times)
                var valueExpression =
                    CreateGetValueExpression(_itemParameter, property, targetType);

                // If the original method expected a different type (e.g., object), add a conversion
                if (valueExpression.Type != methodCallExpression.Type)
                    return Convert(valueExpression, methodCallExpression.Type);

                return valueExpression;
            }
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    /// <summary>
    /// Creates an expression tree that extracts a value from Dictionary&lt;string, AttributeValue&gt;.
    ///
    /// <para>
    /// This method builds expression trees at query compilation time (not runtime).
    /// The resulting expression is compiled once per query shape, then executed many times.
    /// </para>
    ///
    /// <para>
    /// Expression tree structure:
    /// 1. Dictionary.TryGetValue(propertyName, out attributeValue)
    /// 2. Check attributeValue.NULL
    /// 3. Extract wire primitive (attributeValue.S, .N, .BOOL, etc.)
    /// 4. Inline EF Core converter expression tree (if present)
    /// 5. Return typed value (NO BOXING!)
    /// </para>
    ///
    /// <para>
    /// To customize parsing formats (e.g., DateTime formats, culture info), users can
    /// provide their own ValueConverter when configuring entities:
    /// <code>
    /// modelBuilder.Entity&lt;MyEntity&gt;()
    ///     .Property(e => e.CreatedAt)
    ///     .HasConversion(new ValueConverter&lt;DateTime, string&gt;(
    ///         dt => dt.ToString("your-format"),
    ///         s => DateTime.ParseExact(s, "your-format", yourCulture)));
    /// </code>
    /// </para>
    /// </summary>
    private static BlockExpression CreateGetValueExpression(
        ParameterExpression itemParameter,
        IProperty property,
        Type type)
    {
        var propertyName = property.Name;
        var typeMapping = property.GetTypeMapping();
        var converter = typeMapping.Converter;

        // Variable to hold the AttributeValue from dictionary lookup
        var attributeValueVariable = Variable(typeof(AttributeValue), "attributeValue");

        // Expression: item.TryGetValue(propertyName, out attributeValue)
        var tryGetValueExpression = Call(
            itemParameter,
            DictionaryTryGetValueMethod,
            Constant(propertyName),
            attributeValueVariable);

        // Expression: attributeValue.NULL == true
        var isNullExpression = Equal(
            Property(attributeValueVariable, AttributeValueNullProperty),
            Constant(true, typeof(bool?)));

        // Build expression for extracting wire primitive from AttributeValue
        var primitiveType = converter?.ProviderClrType ?? type;
        var primitiveExpression = CreateAttributeValueToPrimitiveExpression(
            attributeValueVariable,
            primitiveType);

        // Inline EF Core converter's expression tree (like Cosmos does!)
        var valueExpression = primitiveExpression;
        if (converter != null)
            // Take the converter's expression tree and inline it directly
            // This eliminates runtime method calls and boxing!
            valueExpression = ReplacingExpressionVisitor.Replace(
                converter.ConvertFromProviderExpression.Parameters.Single(),
                primitiveExpression,
                converter.ConvertFromProviderExpression.Body);

        // Convert to target type if needed
        if (valueExpression.Type != type)
            valueExpression = Convert(valueExpression, type);

        // Build the complete expression with null handling:
        // tryGetValue ? (attributeValue.NULL ? default : value) : default
        var completeExpression = Condition(
            tryGetValueExpression,
            Condition(isNullExpression, Default(type), valueExpression),
            Default(type));

        // Wrap in block with attributeValue variable
        return Block([attributeValueVariable], completeExpression);
    }

    /// <summary>
    /// Creates an expression tree that converts AttributeValue to a CLR wire primitive.
    ///
    /// <para>
    /// This builds expression trees like:
    /// - string: attributeValue.S
    /// - bool: attributeValue.BOOL ?? false
    /// - long: long.Parse(attributeValue.N, CultureInfo.InvariantCulture)
    /// - etc.
    /// </para>
    ///
    /// <para>
    /// These expressions are inlined into the final compiled query, eliminating
    /// runtime method calls and boxing operations.
    /// </para>
    /// </summary>
    private static Expression CreateAttributeValueToPrimitiveExpression(
        Expression attributeValueExpression,
        Type primitiveType)
    {
        // String: attributeValue.S
        if (primitiveType == typeof(string))
            return Property(attributeValueExpression, AttributeValueSProperty);

        // Bool: attributeValue.BOOL ?? false
        if (primitiveType == typeof(bool))
            return Coalesce(
                Property(attributeValueExpression, AttributeValueBoolProperty),
                Constant(false));

        // Byte array: attributeValue.B?.ToArray()
        if (primitiveType == typeof(byte[]))
        {
            var bProperty = Property(attributeValueExpression, AttributeValueBProperty);
            return Condition(
                Equal(bProperty, Constant(null, typeof(MemoryStream))),
                Constant(null, typeof(byte[])),
                Call(bProperty, MemoryStreamToArrayMethod));
        }

        // Numeric types: parse from attributeValue.N
        var nProperty = Property(attributeValueExpression, AttributeValueNProperty);
        var cultureInfo = Constant(CultureInfo.InvariantCulture);

        if (primitiveType == typeof(long))
            return Call(LongParseMethod, nProperty, cultureInfo);

        if (primitiveType == typeof(double))
            return Call(DoubleParseMethod, nProperty, cultureInfo);

        if (primitiveType == typeof(decimal))
            return Call(DecimalParseMethod, nProperty, cultureInfo);

        throw new InvalidOperationException(
            $"Cannot create expression for AttributeValue to primitive type '{primitiveType.Name}'. "
            + $"Supported wire primitives: string, bool, long, double, decimal, byte[]");
    }
}
