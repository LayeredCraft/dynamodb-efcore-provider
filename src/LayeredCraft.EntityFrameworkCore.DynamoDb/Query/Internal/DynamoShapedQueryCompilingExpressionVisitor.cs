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
    private int _runtimeParameterIndex;

    private static readonly MethodInfo EnsurePositivePageSizeMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(EnsurePositivePageSize))!;

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

        if (selectExpression.ResultLimitExpression is not null)
            selectExpression.ApplyResultLimitExpression(
                NormalizeRuntimeIntExpression(
                    selectExpression.ResultLimitExpression,
                    "resultLimit",
                    false));

        if (selectExpression.PageSizeExpression is not null)
            selectExpression.ApplyPageSizeExpression(
                NormalizeRuntimeIntExpression(
                    selectExpression.PageSizeExpression,
                    "pageSize",
                    true));

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

    private Expression NormalizeRuntimeIntExpression(
        Expression expression,
        string parameterNamePrefix,
        bool requirePositive)
    {
        if (expression is ConstantExpression { Value: int })
            return expression;

        if (expression is QueryParameterExpression)
            return expression;

        var parameterName = $"__dynamo_{parameterNamePrefix}_{_runtimeParameterIndex++}";
        var injectedExpression = new DynamoInjectingExpressionVisitor().Visit(expression)
            ?? throw new InvalidOperationException(
                $"Unable to normalize {parameterNamePrefix} expression.");

        var convertedExpression = Expression.Convert(injectedExpression, typeof(int));
        Expression body = convertedExpression;
        if (requirePositive)
            body = Expression.Call(EnsurePositivePageSizeMethodInfo, convertedExpression);

        var valueExtractor = Expression.Lambda(body, QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(parameterName, valueExtractor);
    }

    private static int EnsurePositivePageSize(int value)
    {
        if (value <= 0)
            throw new InvalidOperationException(
                "WithPageSize must evaluate to a positive integer.");

        return value;
    }
}
