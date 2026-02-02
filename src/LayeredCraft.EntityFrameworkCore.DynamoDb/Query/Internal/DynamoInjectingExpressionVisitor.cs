using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Injects Dictionary&lt;string, AttributeValue&gt; parameter handling into the expression
///     tree. This visitor runs BEFORE InjectStructuralTypeMaterializers to prepare the expression tree
///     by adding null-checking and casting for structural types. Similar to
///     JObjectInjectingExpressionVisitor (Cosmos) and BsonDocumentInjectingExpressionVisitor
///     (MongoDB).
/// </summary>
public class DynamoInjectingExpressionVisitor : ExpressionVisitor
{
    private static readonly MethodInfo GetParameterValueMethodInfo =
        typeof(DynamoInjectingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(GetParameterValue))!;

    /// <summary>Rewrites query parameter expressions to access runtime values.</summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryParameterExpression queryParameterExpression)
            return CreateParameterValueExpression(queryParameterExpression);

        return base.VisitExtension(node);
    }

    /// <summary>Creates a query context parameter access expression for a captured value.</summary>
    private static MethodCallExpression CreateParameterValueExpression(
        QueryParameterExpression queryParameterExpression)
    {
        var parameterName = Expression.Constant(queryParameterExpression.Name);

        return Expression.Call(
            GetParameterValueMethodInfo.MakeGenericMethod(queryParameterExpression.Type),
            QueryCompilationContext.QueryContextParameter,
            parameterName);
    }

    /// <summary>Reads a typed parameter value from the query context.</summary>
    private static T GetParameterValue<T>(QueryContext queryContext, string parameterName)
        => (T)queryContext.Parameters[parameterName]!;
}
