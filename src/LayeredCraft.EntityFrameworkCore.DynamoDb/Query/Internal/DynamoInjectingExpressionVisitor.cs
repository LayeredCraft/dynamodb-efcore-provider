using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Rewrites QueryParameterExpression nodes to read runtime values from the query context.
///     This visitor runs BEFORE InjectStructuralTypeMaterializers to prepare the expression tree
///     with parameter access expressions. Similar to JObjectInjectingExpressionVisitor (Cosmos)
///     and BsonDocumentInjectingExpressionVisitor (MongoDB).
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

        if (node is DynamoCollectionShaperExpression collectionShaperExpression)
            return collectionShaperExpression.Update(
                Visit(collectionShaperExpression.Projection),
                Visit(collectionShaperExpression.InnerShaper));

        // Projection placeholders are consumed later during projection-binding removal; there is
        // no query-context injection work to perform for them at this stage.
        if (node is DynamoObjectArrayProjectionExpression)
            return node;

        // Keep EF Core's sentinel untouched so upstream translation fallback still works.
        if (ReferenceEquals(node, QueryCompilationContext.NotTranslatedExpression))
            return node;

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
