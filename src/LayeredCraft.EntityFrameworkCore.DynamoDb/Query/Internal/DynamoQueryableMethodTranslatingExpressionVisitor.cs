using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryableMethodTranslatingExpressionVisitor
    : QueryableMethodTranslatingExpressionVisitor
{
    private readonly DynamoSqlTranslatingExpressionVisitor _sqlTranslator;
    private readonly DynamoProjectionBindingExpressionVisitor _projectionBindingExpressionVisitor;
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DynamoQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        ISqlExpressionFactory sqlExpressionFactory) : base(
        dependencies,
        queryCompilationContext,
        false)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _sqlTranslator = new DynamoSqlTranslatingExpressionVisitor(sqlExpressionFactory);
        _projectionBindingExpressionVisitor = new DynamoProjectionBindingExpressionVisitor(
            _sqlTranslator,
            sqlExpressionFactory,
            queryCompilationContext.Model);
    }

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => throw new NotImplementedException();

    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        var method = methodCallExpression.Method;

        // Check for DynamoDB-specific extension methods
        if (method.DeclaringType == typeof(DynamoDbQueryableExtensions))
        {
            if (method.Name == nameof(DynamoDbQueryableExtensions.WithPageSize))
            {
                var context = (DynamoQueryCompilationContext)QueryCompilationContext;
                // The outermost call is the last chained call, so capture once to keep last-wins
                // semantics.
                if (context.PageSizeOverrideExpression == null)
                {
                    context.PageSizeOverrideExpression = methodCallExpression.Arguments[1];
                    if (methodCallExpression.Arguments[1] is ConstantExpression
                        {
                            Value: int pageSize,
                        })
                        context.PageSizeOverride = pageSize;
                }

                // Continue visiting the source (prune this extension from the tree)
                return Visit(methodCallExpression.Arguments[0]);
            }

            if (method.Name == nameof(DynamoDbQueryableExtensions.WithoutPagination))
            {
                var context = (DynamoQueryCompilationContext)QueryCompilationContext;
                if (!context.PaginationDisabled)
                    context.PaginationDisabled = true;

                // Continue visiting the source (prune this extension from the tree)
                return Visit(methodCallExpression.Arguments[0]);
            }
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    protected override ShapedQueryExpression? CreateShapedQueryExpression(IEntityType entityType)
    {
        // Get the table name from entity metadata
        var tableName = entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string
            ?? entityType.ClrType.Name;

        var queryExpression = new SelectExpression(tableName);

        // Create entity projection expression as single source of truth for property mapping
        var entityProjection =
            new DynamoEntityProjectionExpression(entityType, _sqlExpressionFactory);

        // Store entity projection in projection mapping under root ProjectionMember
        var projectionMapping = new Dictionary<ProjectionMember, Expression>
        {
            [new ProjectionMember()] = entityProjection,
        };

        queryExpression.ReplaceProjectionMapping(projectionMapping);

        var projectionBindingExpression = new ProjectionBindingExpression(
            queryExpression,
            new ProjectionMember(),
            typeof(ValueBuffer));

        var structuralTypeShaperExpression =
            new StructuralTypeShaperExpression(entityType, projectionBindingExpression, false);

        return new ShapedQueryExpression(queryExpression, structuralTypeShaperExpression);
    }

    protected override ShapedQueryExpression? TranslateAll(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateAny(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateCast(
        ShapedQueryExpression source,
        Type castType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateConcat(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateContains(
        ShapedQueryExpression source,
        Expression item)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateDefaultIfEmpty(
        ShapedQueryExpression source,
        Expression? defaultValue)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateExcept(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateFirstOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
    {
        if (predicate != null)
        {
            if (TranslateWhere(source, predicate) is not { } translatedSource)
                return null;

            source = translatedSource;
        }

        var selectExpression = (SelectExpression)source.QueryExpression;

        // Set result limit (how many to return to caller)
        selectExpression.ApplyOrCombineResultLimitExpression(Expression.Constant(1));

        var context = (DynamoQueryCompilationContext)QueryCompilationContext;
        ApplyPageSize(selectExpression, context);

        return source;
    }

    protected override ShapedQueryExpression? TranslateGroupBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        LambdaExpression? elementSelector,
        LambdaExpression? resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateIntersect(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateRightJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateLongCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateMin(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateOfType(
        ShapedQueryExpression source,
        Type resultType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateOrderBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, keySelector);

        if (translation == null)
            return null;

        selectExpression.ApplyOrdering(new OrderingExpression(translation, ascending));
        return source;
    }

    protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression TranslateSelect(
        ShapedQueryExpression source,
        LambdaExpression selector)
    {
        // Optimization: identity projection x => x
        if (selector.Body == selector.Parameters[0])
            return source;

        var selectExpression = (SelectExpression)source.QueryExpression;

        // Remap lambda body: replace parameter with current shaper
        var newSelectorBody = ReplacingExpressionVisitor.Replace(
            selector.Parameters[0],
            source.ShaperExpression,
            selector.Body);

        // Delegate to projection binding visitor
        var newShaper = _projectionBindingExpressionVisitor.Translate(
            selectExpression,
            newSelectorBody);

        return source.UpdateShaperExpression(newShaper);
    }

    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression collectionSelector,
        LambdaExpression resultSelector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression selector)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateSkip(
        ShapedQueryExpression source,
        Expression count)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateSkipWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateSum(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateTake(
        ShapedQueryExpression source,
        Expression count)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;

        // Store the expression for later evaluation (handles both constants and parameters)
        selectExpression.ApplyOrCombineResultLimitExpression(count);

        var context = (DynamoQueryCompilationContext)QueryCompilationContext;
        ApplyPageSize(selectExpression, context);

        return source;
    }

    /// <summary>
    ///     Determines the page size to use based on configuration hierarchy: per-query override →
    ///     global default → null (DynamoDB default of 1MB).
    /// </summary>
    private void ApplyPageSize(
        SelectExpression selectExpression,
        DynamoQueryCompilationContext context)
    {
        if (context.PageSizeOverrideExpression is not null)
        {
            selectExpression.ApplyPageSizeExpression(context.PageSizeOverrideExpression);
            return;
        }

        if (context.PageSizeOverride.HasValue)
        {
            selectExpression.ApplyPageSize(context.PageSizeOverride.Value);
            return;
        }

        selectExpression.ApplyPageSize(DetermineDefaultPageSize(context));
    }

    /// <summary>Determines the default page size to use when no per-query override is set.</summary>
    private int? DetermineDefaultPageSize(DynamoQueryCompilationContext context)
    {
        var options = context.ContextOptions.FindExtension<DynamoDbOptionsExtension>();
        return options?.DefaultPageSize;
    }

    protected override ShapedQueryExpression? TranslateTakeWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateThenBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, keySelector);

        if (translation == null)
            return null;

        selectExpression.AppendOrdering(new OrderingExpression(translation, ascending));
        return source;
    }

    protected override ShapedQueryExpression? TranslateUnion(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? TranslateWhere(
        ShapedQueryExpression source,
        LambdaExpression predicate)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, predicate);

        if (translation == null)
            return null;

        translation = NormalizePredicate(translation);

        selectExpression.ApplyPredicate(translation);
        return source;
    }

    /// <summary>
    /// Translates a lambda expression by translating its body.
    /// </summary>
    private SqlExpression? TranslateLambdaExpression(
            ShapedQueryExpression shapedQueryExpression,
            LambdaExpression lambdaExpression)
        // The lambda parameter represents the entity being queried (e.g., x in x => x.Pk == "test")
        // The SQL translator will recognize parameter.Property accesses and convert them to
        // SqlPropertyExpression
        => _sqlTranslator.Translate(lambdaExpression.Body);

    /// <summary>Normalizes boolean predicates into explicit comparisons for PartiQL.</summary>
    private SqlExpression NormalizePredicate(SqlExpression expression)
        => NormalizePredicate(expression, _sqlExpressionFactory, true);

    /// <summary>Normalizes search-condition terms while preserving normal comparisons.</summary>
    private static SqlExpression NormalizePredicate(
        SqlExpression expression,
        ISqlExpressionFactory sqlExpressionFactory,
        bool inSearchCondition)
        => expression switch
        {
            SqlBinaryExpression binaryExpression => NormalizeBinary(
                binaryExpression,
                sqlExpressionFactory),
            SqlPropertyExpression propertyExpression when inSearchCondition
                && IsBooleanType(propertyExpression.Type) => WrapBooleanPredicate(
                    propertyExpression,
                    sqlExpressionFactory),
            SqlParameterExpression parameterExpression when inSearchCondition
                && IsBooleanType(parameterExpression.Type) => WrapBooleanPredicate(
                    parameterExpression,
                    sqlExpressionFactory),
            _ => expression,
        };

    /// <summary>Recursively normalizes logical predicates while preserving comparisons.</summary>
    private static SqlBinaryExpression NormalizeBinary(
        SqlBinaryExpression binaryExpression,
        ISqlExpressionFactory sqlExpressionFactory)
    {
        // Search conditions only exist at AND/OR boundaries.
        if (binaryExpression.OperatorType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            var left = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, true);
            var right = NormalizePredicate(binaryExpression.Right, sqlExpressionFactory, true);
            return binaryExpression.Update(left, right);
        }

        // Avoid rewriting operands inside explicit comparisons.
        if (IsComparisonOperator(binaryExpression.OperatorType))
        {
            var left = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, false);
            var right = NormalizePredicate(binaryExpression.Right, sqlExpressionFactory, false);
            return binaryExpression.Update(left, right);
        }

        var normalizedLeft = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, false);
        var normalizedRight = NormalizePredicate(
            binaryExpression.Right,
            sqlExpressionFactory,
            false);
        return binaryExpression.Update(normalizedLeft, normalizedRight);
    }

    /// <summary>Wraps a boolean column/parameter into an explicit comparison.</summary>
    private static SqlExpression WrapBooleanPredicate(
        SqlExpression expression,
        ISqlExpressionFactory sqlExpressionFactory)
        => sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                expression,
                sqlExpressionFactory.Constant(true, typeof(bool)))
            ?? expression;

    private static bool IsBooleanType(Type type) => type == typeof(bool) || type == typeof(bool?);

    private static bool IsComparisonOperator(ExpressionType operatorType)
        => operatorType is ExpressionType.Equal
            or ExpressionType.NotEqual
            or ExpressionType.LessThan
            or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan
            or ExpressionType.GreaterThanOrEqual;
}
