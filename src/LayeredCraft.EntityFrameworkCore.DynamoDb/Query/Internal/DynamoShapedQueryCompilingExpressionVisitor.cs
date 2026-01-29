using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    DynamoQuerySqlGenerator sqlGenerator,
    ISqlExpressionFactory sqlExpressionFactory) : ShapedQueryCompilingExpressionVisitor(
    dependencies,
    dynamoQueryCompilationContext)
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;

    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var shaperBody = shapedQueryExpression.ShaperExpression;
        var entityType =
            (shaperBody as StructuralTypeShaperExpression)?.StructuralType as IEntityType;

        if (entityType == null)
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

        var methodInfo = dynamoQueryCompilationContext.IsAsync
            ? TranslateAndExecuteQueryAsyncMethodInfo
            : TranslateAndExecuteQueryMethodInfo;

        var call = Expression.Call(
            methodInfo.MakeGenericMethod(shaperBody.Type),
            queryContextParameter,
            Expression.Constant(selectExpression),
            Expression.Constant(sqlGenerator),
            Expression.Constant(sqlExpressionFactory),
            shaperLambda,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));

        return call;
    }

    private static readonly MethodInfo TranslateAndExecuteQueryMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .DeclaredMethods.Single(m => m.Name == nameof(TranslateAndExecuteQuery));

    private static readonly MethodInfo TranslateAndExecuteQueryAsyncMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .DeclaredMethods.Single(m => m.Name == nameof(TranslateAndExecuteQueryAsync));

    private static QueryingEnumerable<T> TranslateAndExecuteQuery<T>(
        DynamoQueryContext queryContext,
        SelectExpression selectExpression,
        DynamoQuerySqlGenerator sqlGenerator,
        ISqlExpressionFactory sqlExpressionFactory,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
    {
        var client = queryContext.Context.GetService<IDynamoClientWrapper>();
        return new QueryingEnumerable<T>(
            queryContext,
            client,
            selectExpression,
            sqlGenerator,
            sqlExpressionFactory,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);
    }

    private static IAsyncEnumerable<T> TranslateAndExecuteQueryAsync<T>(
        DynamoQueryContext queryContext,
        SelectExpression selectExpression,
        DynamoQuerySqlGenerator sqlGenerator,
        ISqlExpressionFactory sqlExpressionFactory,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
    {
        var client = queryContext.Context.GetService<IDynamoClientWrapper>();
        return new QueryingEnumerable<T>(
            queryContext,
            client,
            selectExpression,
            sqlGenerator,
            sqlExpressionFactory,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);
    }
}
