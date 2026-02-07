using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
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
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;

        // Finalize projection mapping → concrete projection list
        selectExpression.ApplyProjection();

        if (selectExpression.PageSize is null && selectExpression.PageSizeExpression is null)
        {
            if (dynamoQueryCompilationContext.PageSizeOverrideExpression is not null)
            {
                selectExpression.ApplyPageSizeExpression(
                    dynamoQueryCompilationContext.PageSizeOverrideExpression);
            }
            else if (dynamoQueryCompilationContext.PageSizeOverride.HasValue)
            {
                selectExpression.ApplyPageSize(
                    dynamoQueryCompilationContext.PageSizeOverride.Value);
            }
            else
            {
                var options = dynamoQueryCompilationContext.ContextOptions
                    .FindExtension<DynamoDbOptionsExtension>();
                if (options?.DefaultPageSize is not null)
                    selectExpression.ApplyPageSize(options.DefaultPageSize);
            }
        }

        var shaperBody = shapedQueryExpression.ShaperExpression;

        // create shaper
        var itemParameter =
            Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "item");

        // Step 1: Inject Dictionary<string, AttributeValue> variable handling
        // This adds null-checking and prepares the expression tree for materialization
        shaperBody = new DynamoInjectingExpressionVisitor().Visit(shaperBody);

        // Step 2: Inject EF Core's standard structural type materializers
        // This adds entity construction and property assignment logic
        shaperBody = InjectStructuralTypeMaterializers(shaperBody);

        // Step 3: Remove projection bindings and replace with actual dictionary access
        // This converts abstract ProjectionBindingExpression to concrete property access
        shaperBody = new DynamoProjectionBindingRemovingExpressionVisitor(
            itemParameter,
            selectExpression).Visit(shaperBody);

        var shaperLambda = Expression.Lambda(
            shaperBody,
            QueryCompilationContext.QueryContextParameter,
            itemParameter);

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
                .Single(c => c.GetParameters().Length == 7),
            queryContextParameter,
            Expression.Constant(selectExpression),
            Expression.Constant(sqlGenerator),
            shaperLambda,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled),
            Expression.Constant(dynamoQueryCompilationContext.PaginationDisabled));
    }
}
