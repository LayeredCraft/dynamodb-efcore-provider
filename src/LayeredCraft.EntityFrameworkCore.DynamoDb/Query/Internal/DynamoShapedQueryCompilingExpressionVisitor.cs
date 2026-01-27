using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext)
    : ShapedQueryCompilingExpressionVisitor(dependencies, dynamoQueryCompilationContext)
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;

    private readonly DynamoQueryCompilationContext _dynamoQueryCompilationContext =
        dynamoQueryCompilationContext;

    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var shaperBody = shapedQueryExpression.ShaperExpression;
        var entityType =
            (shaperBody as StructuralTypeShaperExpression)?.StructuralType as IEntityType;

        if (entityType == null)
            throw new InvalidOperationException(
                "Dynamo MVP only supports entity queries with a structural shaper.");

        var tableName =
            entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string
            ?? entityType.ClrType.Name;

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

        var partiQl = $"SELECT * FROM {tableName}";
        var parameters = Array.Empty<AttributeValue>();

        var queryContextParameter = Expression.Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var standAloneStateManager =
            _dynamoQueryCompilationContext.QueryTrackingBehavior
            == QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        var methodInfo = _dynamoQueryCompilationContext.IsAsync
            ? TranslateAndExecuteQueryAsyncMethodInfo
            : TranslateAndExecuteQueryMethodInfo;

        var call = Expression.Call(
            methodInfo.MakeGenericMethod(shaperBody.Type),
            queryContextParameter,
            Expression.Constant(partiQl),
            Expression.Constant(parameters, typeof(IReadOnlyList<AttributeValue>)),
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

    private static IEnumerable<T> TranslateAndExecuteQuery<T>(
        DynamoQueryContext queryContext,
        string partiQl,
        IReadOnlyList<AttributeValue> parameters,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
    {
        var client = queryContext.Context.GetService<IDynamoClientWrapper>();
        return new QueryingEnumerable<T>(
            queryContext,
            client,
            partiQl,
            parameters,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);
    }

    private static IAsyncEnumerable<T> TranslateAndExecuteQueryAsync<T>(
        DynamoQueryContext queryContext,
        string partiQl,
        IReadOnlyList<AttributeValue> parameters,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
    {
        var client = queryContext.Context.GetService<IDynamoClientWrapper>();
        return new QueryingEnumerable<T>(
            queryContext,
            client,
            partiQl,
            parameters,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);
    }
}
