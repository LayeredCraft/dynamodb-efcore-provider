using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Replaces EF Core's abstract ProjectionBindingExpression nodes with concrete expression trees
///     that extract property values from Dictionary&lt;string, AttributeValue&gt;.
/// </summary>
/// <remarks>
///     Builds expression trees at query compilation time, inlining all type conversions to eliminate
///     runtime boxing. The compiled query executes AttributeValue deserialization and EF Core value
///     conversions as pure IL with zero boxing overhead.
/// </remarks>
public class DynamoProjectionBindingRemovingExpressionVisitor(ParameterExpression itemParameter)
    : ExpressionVisitor
{
    // Reflection cache for efficient expression tree construction
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
        typeof(Dictionary<string, AttributeValue>).GetMethod(nameof(Dictionary<,>.TryGetValue))!;

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

    /// <summary>
    ///     Intercepts MaterializationContext constructor calls to replace ProjectionBindingExpression
    ///     with ValueBuffer.Empty placeholder (actual data comes from Dictionary access).
    /// </summary>
    protected override Expression VisitNew(NewExpression node)
    {
        if (node.Arguments.Count > 0 && node.Arguments[0] is ProjectionBindingExpression)
        {
            // new MaterializationContext(ValueBuffer.Empty, ...)
            List<Expression> newArguments = [Constant(ValueBuffer.Empty)];

            for (var i = 1; i < node.Arguments.Count; i++)
                newArguments.Add(Visit(node.Arguments[i]));

            return node.Update(newArguments);
        }

        return base.VisitNew(node);
    }

    /// <summary>
    ///     Intercepts ValueBufferTryReadValue calls and replaces them with inline expression trees
    ///     that extract values from Dictionary&lt;string, AttributeValue&gt; with zero boxing overhead.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.IsGenericMethod)
        {
            var genericMethod = node.Method.GetGenericMethodDefinition();

            if (genericMethod == ExpressionExtensions.ValueBufferTryReadValueMethod)
            {
                var property = (IProperty)((ConstantExpression)node.Arguments[2]).Value!;
                var targetType = node.Type == typeof(object) ? property.ClrType : node.Type;

                // Build inline expression: item.TryGetValue(...) ? value : default
                var valueExpression = CreateGetValueExpression(itemParameter, property, targetType);

                return valueExpression.Type != node.Type
                    ? Convert(valueExpression, node.Type)
                    : valueExpression;
            }
        }

        return base.VisitMethodCall(node);
    }

    /// <summary>
    ///     Builds an expression tree that extracts a typed value from Dictionary&lt;string, AttributeValue&gt;
    ///     with null handling, wire primitive extraction, and inlined EF Core converter application.
    /// </summary>
    /// <remarks>
    ///     Expression structure:
    ///     <list type="number">
    ///         <item>Dictionary.TryGetValue(propertyName, out attributeValue)</item>
    ///         <item>Check attributeValue.NULL flag</item>
    ///         <item>Extract wire primitive (attributeValue.S, .N, .BOOL, etc.)</item>
    ///         <item>Inline EF Core converter expression tree (if present)</item>
    ///         <item>Return typed value with zero boxing</item>
    ///     </list>
    /// </remarks>
    private static BlockExpression CreateGetValueExpression(
        ParameterExpression itemParameter,
        IProperty property,
        Type type)
    {
        var propertyName = property.Name;
        var typeMapping = property.GetTypeMapping();
        var converter = typeMapping.Converter;

        var attributeValueVariable = Variable(typeof(AttributeValue), "attributeValue");

        // item.TryGetValue("PropertyName", out attributeValue)
        var tryGetValueExpression = Call(
            itemParameter,
            DictionaryTryGetValueMethod,
            Constant(propertyName),
            attributeValueVariable);

        // attributeValue.NULL == true
        var isNullExpression = Equal(
            Property(attributeValueVariable, AttributeValueNullProperty),
            Constant(true, typeof(bool?)));

        // Extract wire primitive: attributeValue.S, long.Parse(attributeValue.N), etc.
        var primitiveType = converter?.ProviderClrType ?? type;
        var primitiveExpression = CreateAttributeValueToPrimitiveExpression(
            attributeValueVariable,
            primitiveType);

        // Inline converter: DateTime.Parse(...), Guid.Parse(...), (int)long.Parse(...), etc.
        var valueExpression = primitiveExpression;
        if (converter != null)
            valueExpression = ReplacingExpressionVisitor.Replace(
                converter.ConvertFromProviderExpression.Parameters.Single(),
                primitiveExpression,
                converter.ConvertFromProviderExpression.Body);

        if (valueExpression.Type != type)
            valueExpression = Convert(valueExpression, type);

        // item.TryGetValue(...) ? (attributeValue.NULL ? default : value) : default
        var completeExpression = Condition(
            tryGetValueExpression,
            Condition(isNullExpression, Default(type), valueExpression),
            Default(type));

        return Block([attributeValueVariable], completeExpression);
    }

    /// <summary>
    ///     Builds an expression tree that deserializes AttributeValue wire format to a CLR primitive type.
    /// </summary>
    /// <remarks>
    ///     Maps AttributeValue properties to CLR types:
    ///     <list type="bullet">
    ///         <item>string → attributeValue.S</item>
    ///         <item>bool → attributeValue.BOOL ?? false</item>
    ///         <item>long → long.Parse(attributeValue.N)</item>
    ///         <item>double → double.Parse(attributeValue.N)</item>
    ///         <item>decimal → decimal.Parse(attributeValue.N)</item>
    ///         <item>byte[] → attributeValue.B?.ToArray()</item>
    ///     </list>
    /// </remarks>
    private static Expression CreateAttributeValueToPrimitiveExpression(
        Expression attributeValueExpression,
        Type primitiveType)
    {
        // attributeValue.S
        if (primitiveType == typeof(string))
            return Property(attributeValueExpression, AttributeValueSProperty);

        // attributeValue.BOOL ?? false
        if (primitiveType == typeof(bool))
            return Coalesce(
                Property(attributeValueExpression, AttributeValueBoolProperty),
                Constant(false));

        // attributeValue.B == null ? null : attributeValue.B.ToArray()
        if (primitiveType == typeof(byte[]))
        {
            var bProperty = Property(attributeValueExpression, AttributeValueBProperty);
            return Condition(
                Equal(bProperty, Constant(null, typeof(MemoryStream))),
                Constant(null, typeof(byte[])),
                Call(bProperty, MemoryStreamToArrayMethod));
        }

        var nProperty = Property(attributeValueExpression, AttributeValueNProperty);
        var cultureInfo = Constant(CultureInfo.InvariantCulture);

        // long.Parse(attributeValue.N, CultureInfo.InvariantCulture)
        if (primitiveType == typeof(long))
            return Call(LongParseMethod, nProperty, cultureInfo);

        // double.Parse(attributeValue.N, CultureInfo.InvariantCulture)
        if (primitiveType == typeof(double))
            return Call(DoubleParseMethod, nProperty, cultureInfo);

        // decimal.Parse(attributeValue.N, CultureInfo.InvariantCulture)
        if (primitiveType == typeof(decimal))
            return Call(DecimalParseMethod, nProperty, cultureInfo);

        throw new InvalidOperationException(
            $"Cannot create expression for AttributeValue to primitive type '{primitiveType.Name}'. "
            + $"Supported wire primitives: string, bool, long, double, decimal, byte[]");
    }
}
