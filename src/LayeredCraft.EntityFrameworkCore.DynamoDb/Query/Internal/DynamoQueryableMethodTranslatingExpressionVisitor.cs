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
        ISqlExpressionFactory sqlExpressionFactory,
        bool subquery = false) : base(
        dependencies,
        queryCompilationContext,
        subquery)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _sqlTranslator = new DynamoSqlTranslatingExpressionVisitor(sqlExpressionFactory);
        _projectionBindingExpressionVisitor = new DynamoProjectionBindingExpressionVisitor(
            _sqlTranslator,
            sqlExpressionFactory,
            queryCompilationContext.Model);
    }

    /// <summary>Creates a translation visitor for subquery pipelines.</summary>
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new DynamoQueryableMethodTranslatingExpressionVisitor(
            Dependencies,
            QueryCompilationContext,
            _sqlExpressionFactory,
            true);

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
        // Get the table name from entity metadata.
        var tableName = GetTableGroupName(entityType);

        var queryExpression = new SelectExpression(tableName);

        // Create entity projection expression as single source of truth for property mapping
        var entityProjection =
            new DynamoEntityProjectionExpression(entityType, _sqlExpressionFactory);

        var discriminatorPredicate = CreateDiscriminatorPredicate(entityType, entityProjection);
        if (discriminatorPredicate is not null)
            queryExpression.SetDeferredDiscriminatorPredicate(discriminatorPredicate);

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

    /// <summary>
    ///     Creates a discriminator predicate for root queries when the table group contains multiple
    ///     concrete entity types.
    /// </summary>
    private SqlExpression? CreateDiscriminatorPredicate(
        IEntityType entityType,
        DynamoEntityProjectionExpression entityProjection)
    {
        if (!RequiresDiscriminatorPredicate(entityType))
            return null;

        var discriminatorProperty = entityType.FindDiscriminatorProperty();
        if (discriminatorProperty is null)
            return null;

        var discriminatorColumn = entityProjection.BindProperty(discriminatorProperty);

        SqlExpression? predicate = null;
        foreach (var concreteType in entityType.GetConcreteDerivedTypesInclusive())
        {
            if (concreteType.IsOwned() || concreteType.ClrType.IsAbstract)
                continue;

            var discriminatorValue = concreteType.GetDiscriminatorValue();
            if (discriminatorValue is null)
                continue;

            var equals = _sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                discriminatorColumn,
                _sqlExpressionFactory.Constant(discriminatorValue, discriminatorProperty.ClrType));

            if (equals is null)
                throw new InvalidOperationException(
                    $"Failed to create discriminator predicate for entity type '{entityType.DisplayName()}'.");

            predicate = predicate is null
                ? equals
                : _sqlExpressionFactory.Binary(ExpressionType.OrElse, predicate, equals)
                ?? throw new InvalidOperationException(
                    $"Failed to compose discriminator predicate for entity type '{entityType.DisplayName()}'.");
        }

        return predicate switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } =>
                new SqlParenthesizedExpression(predicate),
            _ => predicate,
        };
    }

    /// <summary>
    ///     Determines whether discriminator filtering is required for the root entity type's table
    ///     group.
    /// </summary>
    private bool RequiresDiscriminatorPredicate(IEntityType entityType)
    {
        var tableGroupName = GetTableGroupName(entityType);

        HashSet<IEntityType> concreteTypes = [];
        foreach (var rootEntityType in QueryCompilationContext
            .Model
            .GetEntityTypes()
            .Where(static t => !t.IsOwned() && t.FindOwnership() is null && t.BaseType is null)
            .Where(t => string.Equals(
                GetTableGroupName(t),
                tableGroupName,
                StringComparison.Ordinal)))
        {
            foreach (var concreteType in rootEntityType.GetConcreteDerivedTypesInclusive())
            {
                if (concreteType.IsOwned() || concreteType.ClrType.IsAbstract)
                    continue;

                concreteTypes.Add(concreteType);
            }
        }

        return concreteTypes.Count > 1;
    }

    /// <summary>Gets the effective table-group name for an entity type.</summary>
    /// <remarks>
    ///     When the current type has no explicit table-name annotation, this resolves against the
    ///     nearest mapped base type so derived queries inherit the root table mapping.
    /// </remarks>
    private static string GetTableGroupName(IReadOnlyEntityType entityType)
    {
        var mappedEntityType = ResolveTableMappedEntityType(entityType);

        return mappedEntityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string
            ?? mappedEntityType.ClrType.Name;
    }

    /// <summary>Resolves the entity type that owns table-name metadata for table-group comparisons.</summary>
    private static IReadOnlyEntityType ResolveTableMappedEntityType(IReadOnlyEntityType entityType)
    {
        if (entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value is string)
            return entityType;

        var current = entityType;
        while (current.BaseType is { } baseType)
        {
            current = baseType;
            if (current.FindAnnotation(DynamoAnnotationNames.TableName)?.Value is string)
                return current;
        }

        return current;
    }

    protected override ShapedQueryExpression? TranslateAll(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => UnsupportedOperator(
            nameof(Queryable.All),
            DynamoStrings.ProviderOperatorNotSupportedYet(nameof(Queryable.All)));

    protected override ShapedQueryExpression? TranslateAny(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.Any),
            DynamoStrings.ProviderOperatorNotSupportedYet(nameof(Queryable.Any)));

    protected override ShapedQueryExpression? TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Average),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Average)));

    protected override ShapedQueryExpression? TranslateCast(
        ShapedQueryExpression source,
        Type castType)
        => UnsupportedOperator(nameof(Queryable.Cast), DynamoStrings.CastNotSupported);

    protected override ShapedQueryExpression? TranslateConcat(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(nameof(Queryable.Concat), DynamoStrings.SetOperationsNotSupported);

    protected override ShapedQueryExpression? TranslateContains(
        ShapedQueryExpression source,
        Expression item)
        => UnsupportedOperator(nameof(Queryable.Contains), DynamoStrings.ContainsNotSupportedYet);

    protected override ShapedQueryExpression? TranslateCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.Count),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Count)));

    protected override ShapedQueryExpression? TranslateDefaultIfEmpty(
        ShapedQueryExpression source,
        Expression? defaultValue)
        => UnsupportedOperator(nameof(Queryable.DefaultIfEmpty), DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
        => UnsupportedOperator(nameof(Queryable.Distinct), DynamoStrings.DistinctNotSupported);

    protected override ShapedQueryExpression? TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault)
        => UnsupportedOperator(
            returnDefault ? nameof(Queryable.ElementAtOrDefault) : nameof(Queryable.ElementAt),
            DynamoStrings.OffsetOperatorsNotSupported);

    protected override ShapedQueryExpression? TranslateExcept(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(nameof(Queryable.Except), DynamoStrings.SetOperationsNotSupported);

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
        => UnsupportedOperator(nameof(Queryable.GroupBy), DynamoStrings.GroupByNotSupported);

    protected override ShapedQueryExpression? TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator(nameof(Queryable.GroupJoin), DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateIntersect(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(
            nameof(Queryable.Intersect),
            DynamoStrings.SetOperationsNotSupported);

    protected override ShapedQueryExpression? TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator(nameof(Queryable.Join), DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator("LeftJoin", DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateRightJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator("RightJoin", DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => UnsupportedOperator(
            returnDefault ? nameof(Queryable.LastOrDefault) : nameof(Queryable.Last),
            DynamoStrings.LastNotSupported);

    protected override ShapedQueryExpression? TranslateLongCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.LongCount),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.LongCount)));

    protected override ShapedQueryExpression? TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Max),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Max)));

    protected override ShapedQueryExpression? TranslateMin(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Min),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Min)));

    protected override ShapedQueryExpression? TranslateOfType(
        ShapedQueryExpression source,
        Type resultType)
        => UnsupportedOperator(nameof(Queryable.OfType), DynamoStrings.OfTypeNotSupportedYet);

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
        => UnsupportedOperator(nameof(Queryable.Reverse), DynamoStrings.ReverseNotSupported);

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
        => UnsupportedOperator(nameof(Queryable.SelectMany), DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression selector)
        => UnsupportedOperator(nameof(Queryable.SelectMany), DynamoStrings.JoinsNotSupported);

    protected override ShapedQueryExpression? TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => UnsupportedOperator(
            returnDefault ? nameof(Queryable.SingleOrDefault) : nameof(Queryable.Single),
            DynamoStrings.ProviderOperatorNotSupportedYet(nameof(Queryable.SingleOrDefault)));

    protected override ShapedQueryExpression? TranslateSkip(
        ShapedQueryExpression source,
        Expression count)
        => UnsupportedOperator(nameof(Queryable.Skip), DynamoStrings.OffsetOperatorsNotSupported);

    protected override ShapedQueryExpression? TranslateSkipWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => UnsupportedOperator(nameof(Queryable.SkipWhile), DynamoStrings.SkipWhileNotSupported);

    protected override ShapedQueryExpression? TranslateSum(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Sum),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Sum)));

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
        => UnsupportedOperator(nameof(Queryable.TakeWhile), DynamoStrings.TakeWhileNotSupported);

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
        => UnsupportedOperator(nameof(Queryable.Union), DynamoStrings.SetOperationsNotSupported);

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
    {
        _ = shapedQueryExpression;
        var translation = _sqlTranslator.Translate(lambdaExpression.Body);
        if (translation != null)
            return translation;

        AddTranslationErrorDetails(
            _sqlTranslator.TranslationErrorDetails ?? DynamoStrings.PredicateNotTranslatable);
        return null;
    }

    /// <summary>
    ///     Registers translation details for an unsupported operator and returns an untranslated
    ///     marker.
    /// </summary>
    private ShapedQueryExpression? UnsupportedOperator(string operatorName, string reason)
    {
        AddTranslationErrorDetails(DynamoStrings.UnsupportedOperator(operatorName, reason));
        return null;
    }

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
