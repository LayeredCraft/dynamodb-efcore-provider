using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

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

    private readonly DynamoScanQueryBehavior _scanQueryBehavior =
        dynamoQueryCompilationContext.ContextOptions.FindExtension<DynamoDbOptionsExtension>()
            ?.ScanQueryBehavior
        ?? DynamoScanQueryBehavior.Throw;

    private int _runtimeParameterIndex;

    private static readonly MethodInfo EnsurePositiveLimitMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(EnsurePositiveLimit))!;

    /// <summary>Builds the runtime querying enumerable and shaper for a translated DynamoDB query.</summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;
        var pagingExpression = shapedQueryExpression.ShaperExpression as DynamoPagingExpression;
        var itemShaperExpression =
            pagingExpression?.InnerShaper ?? shapedQueryExpression.ShaperExpression;

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

        var shaperBody = itemShaperExpression;

        // create shaper
        var itemParameter =
            Parameter(typeof(Dictionary<string, AttributeValue>), "item");

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

        var queryContextParameter = Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var standAloneStateManager = dynamoQueryCompilationContext.QueryTrackingBehavior
            == QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        if (!dynamoQueryCompilationContext.IsAsync)
            throw new InvalidOperationException(
                "Synchronous query execution is not supported for DynamoDB. Use async methods (e.g. ToListAsync). ");

        if (pagingExpression is not null)
            return CreatePagingEnumerableExpression(
                shaperBody.Type,
                queryContextParameter,
                selectExpression,
                shaperLambda,
                standAloneStateManager);

        return New(
            typeof(QueryingEnumerable<>)
                .MakeGenericType(shaperBody.Type)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 7),
            queryContextParameter,
            Constant(selectExpression),
            Constant(_scanQueryBehavior),
            Constant(sqlGeneratorFactory),
            shaperLambda,
            Constant(standAloneStateManager),
            Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));
    }

    private Expression CreatePagingEnumerableExpression(
        Type shaperType,
        UnaryExpression queryContextParameter,
        SelectExpression selectExpression,
        LambdaExpression shaperLambda,
        bool standAloneStateManager)
        => New(
            typeof(PagingQueryingEnumerable<>)
                .MakeGenericType(shaperType)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 7),
            queryContextParameter,
            Constant(selectExpression),
            Constant(_scanQueryBehavior),
            Constant(sqlGeneratorFactory),
            shaperLambda,
            Constant(standAloneStateManager),
            Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));

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
        var convertedExpression = Convert(injectedExpression, typeof(int));

        // Validate at runtime: Limit must be positive.
        var body = Call(EnsurePositiveLimitMethodInfo, convertedExpression);
        var valueExtractor = Expression.Lambda(body, QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(parameterName, valueExtractor);
    }

    /// <summary>
    ///     Emits complex property and complex collection initialization markers for each
    ///     complex property on the structural type being materialized.
    /// </summary>
    /// <remarks>
    ///     Called by <see cref="DynamoStructuralTypeMaterializerSource" /> for each structural type when
    ///     <c>ReadComplexTypeDirectly</c> returns <see langword="false" />.
    ///     The markers are later processed by
    ///     <see cref="DynamoProjectionBindingRemovingExpressionVisitor" />, which pushes the correct
    ///     nested <c>Dictionary&lt;string, AttributeValue&gt;</c> context onto the attribute stack
    ///     before visiting the injected scalar materializer.
    /// </remarks>
    public override void AddStructuralTypeInitialization(
        StructuralTypeShaperExpression shaper,
        ParameterExpression instanceVariable,
        List<ParameterExpression> variables,
        List<Expression> expressions)
    {
        foreach (var complexProperty in shaper.StructuralType.GetComplexProperties())
        {
            var member = MakeMemberAccess(
                instanceVariable,
                complexProperty.GetMemberInfo(forMaterialization: true, forSet: true));

            if (complexProperty.IsCollection)
            {
                // Inject per-element materializer for the complex element type.
                var elementShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Constant(ValueBuffer.Empty),
                    false);
                var elementMaterializer = InjectStructuralTypeMaterializers(elementShaper);
                expressions.Add(
                    new DynamoComplexCollectionInitializationExpression(
                        complexProperty,
                        elementMaterializer,
                        member));
            }
            else
            {
                // Inject the scalar materializer for the complex type's own properties.
                // Nested complex properties within this type will recursively emit further markers
                // via AddStructuralTypeInitialization calls during injection.
                var complexShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Constant(ValueBuffer.Empty),
                    complexProperty.IsNullable);
                var injectedMaterializer = InjectStructuralTypeMaterializers(complexShaper);
                expressions.Add(
                    new DynamoComplexPropertyInitializationExpression(
                        complexProperty,
                        injectedMaterializer,
                        member));
            }
        }
    }

    /// <summary>Validates that the runtime Limit value is positive.</summary>
    private static int EnsurePositiveLimit(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException("limit", "Limit must be a positive integer.");

        return value;
    }
}
