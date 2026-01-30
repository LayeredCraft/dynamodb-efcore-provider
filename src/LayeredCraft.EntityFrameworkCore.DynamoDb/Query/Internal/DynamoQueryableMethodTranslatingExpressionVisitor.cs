using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryableMethodTranslatingExpressionVisitor(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    QueryCompilationContext queryCompilationContext,
    ISqlExpressionFactory sqlExpressionFactory)
    : QueryableMethodTranslatingExpressionVisitor(dependencies, queryCompilationContext, false)
{
    private readonly DynamoSqlTranslatingExpressionVisitor _sqlTranslator =
        new(sqlExpressionFactory, queryCompilationContext);

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => throw new NotImplementedException();

    protected override ShapedQueryExpression? CreateShapedQueryExpression(IEntityType entityType)
    {
        // Get the table name from entity metadata
        var tableName = entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string
                        ?? entityType.ClrType.Name;

        var queryExpression = new SelectExpression(tableName);

        // Add projections for all entity properties
        // This ensures SELECT column1, column2, ... instead of SELECT *
        foreach (var property in entityType.GetProperties())
        {
            var propertyName = property.Name; // Use CLR property name
            var sqlProperty = sqlExpressionFactory.Property(propertyName, property.ClrType);
            var projection = new ProjectionExpression(sqlProperty, propertyName);
            queryExpression.AddToProjection(projection);
        }

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
        => throw new NotImplementedException();

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
        => throw new NotImplementedException();

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
        => throw new NotImplementedException();

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
}
