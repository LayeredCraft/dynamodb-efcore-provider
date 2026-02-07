using System.Linq.Expressions;
using System.Reflection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Evaluates an expression to extract an integer value. Handles constants, parameters, and
///     closure variables.
/// </summary>
internal static class ParameterExpressionEvaluator
{
    public static int EvaluateInt(Expression expression, Dictionary<string, object?> parameters)
    {
        // Try direct evaluation first (handles most cases)
        var value = EvaluateExpression(expression, parameters);

        if (value is int intValue)
            return intValue;

        throw new InvalidOperationException(
            $"Unable to evaluate expression to integer. Got {value?.GetType().Name ?? "null"}");
    }

    private static object? EvaluateExpression(
        Expression expression,
        Dictionary<string, object?> parameters)
    {
        // Handle EF Core's QueryParameterExpression (extension node)
        if (expression.NodeType == ExpressionType.Extension
            && expression.GetType().Name == "QueryParameterExpression")
        {
            // Use reflection to get the Name property
            var nameProperty = expression.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                var paramName = (string?)nameProperty.GetValue(expression);
                if (paramName != null && parameters.TryGetValue(paramName, out var paramValue))
                    return paramValue;
            }
        }

        switch (expression)
        {
            case ConstantExpression constant:
                return constant.Value;

            case MemberExpression member when member.Expression is ConstantExpression constantExpr:
                // Closure variable (e.g., from lambda capture)
                var container = constantExpr.Value;
                if (member.Member is FieldInfo field)
                    return field.GetValue(container);

                if (member.Member is PropertyInfo property)
                    return property.GetValue(container);

                break;

            case MemberExpression member:
                // Evaluate the object first, then get the member
                var obj = EvaluateExpression(member.Expression!, parameters);
                if (member.Member is FieldInfo fieldInfo)
                    return fieldInfo.GetValue(obj);

                if (member.Member is PropertyInfo propertyInfo)
                    return propertyInfo.GetValue(obj);

                break;

            case UnaryExpression unary:
                // Handle conversions
                var operandValue = EvaluateExpression(unary.Operand, parameters);
                if (unary.NodeType == ExpressionType.Convert
                    || unary.NodeType == ExpressionType.ConvertChecked)
                    return Convert.ChangeType(operandValue, unary.Type);

                break;
        }

        // Fallback: try to compile and execute (may fail with EF extension nodes)
        try
        {
            var lambda =
                Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)));
            return lambda.Compile()();
        }
        catch
        {
            throw new InvalidOperationException(
                $"Unable to evaluate expression of type {expression.GetType().Name}");
        }
    }
}
