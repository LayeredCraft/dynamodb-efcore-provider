using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoShapedQueryCompilingExpressionVisitor type.</summary>
public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory) : ShapedQueryCompilingExpressionVisitor(
    dependencies,
    dynamoQueryCompilationContext)
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;
    private int _runtimeParameterIndex;

    private static readonly MethodInfo EnsurePositiveLimitMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(EnsurePositiveLimit))!;

    /// <summary>Builds the runtime querying enumerable and shaper for a translated DynamoDB query.</summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;

        // Discriminator-predicate finalisation, projection finalisation, and index selection are
        // performed earlier in the pipeline by DynamoQueryTranslationPostprocessor so the analyzer
        // sees the complete predicate tree and projection shape. By this point SelectExpression
        // already has IndexName set (or null for base-table queries).

        // Normalize parameterized Limit(n) expression for runtime evaluation.
        // Constant values are already inline; only runtime parameters need registration.
        if (selectExpression.LimitExpression is not null
            && selectExpression.LimitExpression is not ConstantExpression)
            selectExpression.ApplyUserLimitExpression(
                NormalizeLimitExpression(selectExpression.LimitExpression));

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
            selectExpression,
            QueryCompilationContext.Model,
            InjectStructuralTypeMaterializers).Visit(shaperBody);

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
                .Single(c => c.GetParameters().Length == 6),
            queryContextParameter,
            Expression.Constant(selectExpression),
            Expression.Constant(sqlGeneratorFactory),
            shaperLambda,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));
    }

    /// <summary>
    ///     Normalizes a parameterized <c>Limit(n)</c> expression for runtime evaluation. Constants
    ///     are returned as-is; all other forms are registered as runtime parameters so EF Core can cache
    ///     and bind query delegates consistently across executions.
    /// </summary>
    private Expression NormalizeLimitExpression(Expression expression)
    {
        if (expression is ConstantExpression { Value: int })
            return expression;

        if (expression is QueryParameterExpression)
            return expression;

        var parameterName = $"__dynamo_limit_{_runtimeParameterIndex++}";
        var injectedExpression = new DynamoInjectingExpressionVisitor().Visit(expression)
            ?? throw new InvalidOperationException("Unable to normalize Limit expression.");

        // Runtime parameters must be int-valued before registration.
        var convertedExpression = Expression.Convert(injectedExpression, typeof(int));

        // Validate at runtime: Limit must be positive.
        var body = Expression.Call(EnsurePositiveLimitMethodInfo, convertedExpression);
        var valueExtractor = Expression.Lambda(body, QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(parameterName, valueExtractor);
    }

    /// <summary>Validates that the runtime Limit value is positive.</summary>
    private static int EnsurePositiveLimit(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException("limit", "Limit must be a positive integer.");

        return value;
    }
}
