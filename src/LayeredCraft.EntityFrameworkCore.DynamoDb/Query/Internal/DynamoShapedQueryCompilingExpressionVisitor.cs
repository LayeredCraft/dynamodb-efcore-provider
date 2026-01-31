using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    DynamoQuerySqlGenerator sqlGenerator) : ShapedQueryCompilingExpressionVisitor(
    dependencies,
    dynamoQueryCompilationContext)
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;

    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var shaperBody = shapedQueryExpression.ShaperExpression;

        if (shaperBody is not StructuralTypeShaperExpression)
            throw new InvalidOperationException(
                "Dynamo MVP only supports entity queries with a structural shaper.");

        // create shaper
        var itemParameter =
            Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "item");

        // Step 1: Inject Dictionary<string, AttributeValue> variable handling
        // This adds null-checking and prepares the expression tree for materialization
        shaperBody = new DynamoInjectingExpressionVisitor(itemParameter).Visit(shaperBody);

        // Step 2: Inject EF Core's standard structural type materializers
        // This adds entity construction and property assignment logic
        shaperBody = InjectStructuralTypeMaterializers(shaperBody);

        // Step 3: Remove projection bindings and replace with actual dictionary access
        // This converts abstract ProjectionBindingExpression to concrete property access
        shaperBody =
            new DynamoProjectionBindingRemovingExpressionVisitor(itemParameter).Visit(shaperBody);

        var shaperLambda = Expression.Lambda(
            shaperBody,
            QueryCompilationContext.QueryContextParameter,
            itemParameter);

        // Pass SelectExpression to QueryingEnumerable for runtime SQL generation
        // This allows parameter inlining to happen at runtime when parameter values are available
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;

        var queryContextParameter = Expression.Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var standAloneStateManager = dynamoQueryCompilationContext.QueryTrackingBehavior
                                     == QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        if (!dynamoQueryCompilationContext.IsAsync)
            throw new InvalidOperationException(
                "Synchronous query execution is not supported for DynamoDB. Use async methods (e.g. ToListAsync). ");

        return Expression.New(
            typeof(QueryingEnumerable<>)
                .MakeGenericType(shaperBody.Type)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 6),
            queryContextParameter,
            Expression.Constant(selectExpression),
            Expression.Constant(sqlGenerator),
            shaperLambda,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));
    }
}
