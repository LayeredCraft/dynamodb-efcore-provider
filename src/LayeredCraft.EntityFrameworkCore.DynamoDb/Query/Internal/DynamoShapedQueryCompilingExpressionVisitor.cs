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
        var shaperExpression = shapedQueryExpression.ShaperExpression;
        var entityType =
            (shaperExpression as StructuralTypeShaperExpression)?.StructuralType as IEntityType;

        if (entityType == null)
            throw new InvalidOperationException(
                "Dynamo MVP only supports entity queries with a structural shaper.");

        var tableName =
            entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string ??
            entityType.ClrType.Name;

        var partiQl = $"SELECT * FROM {tableName}";
        var parameters = Array.Empty<AttributeValue>();

        var queryContextParameter = Expression.Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var shaperDelegate = CreateMaterializerDelegate(shaperExpression.Type);

        var standAloneStateManager = _dynamoQueryCompilationContext.QueryTrackingBehavior ==
                                     QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        var methodInfo = _dynamoQueryCompilationContext.IsAsync
            ? TranslateAndExecuteQueryAsyncMethodInfo
            : TranslateAndExecuteQueryMethodInfo;

        var call = Expression.Call(
            methodInfo.MakeGenericMethod(shaperExpression.Type),
            queryContextParameter,
            Expression.Constant(partiQl),
            Expression.Constant(parameters, typeof(IReadOnlyList<AttributeValue>)),
            shaperDelegate,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));

        return call;
    }

    private static readonly MethodInfo TranslateAndExecuteQueryMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor).GetTypeInfo()
            .DeclaredMethods.Single(m => m.Name == nameof(TranslateAndExecuteQuery));

    private static readonly MethodInfo TranslateAndExecuteQueryAsyncMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor).GetTypeInfo()
            .DeclaredMethods.Single(m => m.Name == nameof(TranslateAndExecuteQueryAsync));

    private static LambdaExpression CreateMaterializerDelegate(Type entityClrType)
    {
        var queryContextParameter = Expression.Parameter(
            typeof(DynamoQueryContext),
            "queryContext");
        var itemParameter = Expression.Parameter(
            typeof(Dictionary<string, AttributeValue>),
            "item");

        var body = Expression.Call(
            MaterializeMethodInfo.MakeGenericMethod(entityClrType),
            itemParameter);

        var funcType = typeof(Func<,,>).MakeGenericType(
            typeof(DynamoQueryContext),
            typeof(Dictionary<string, AttributeValue>),
            entityClrType);

        return Expression.Lambda(funcType, body, queryContextParameter, itemParameter);
    }

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

    private static readonly MethodInfo MaterializeMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor).GetTypeInfo()
            .DeclaredMethods.Single(m => m.Name == nameof(Materialize));

    private static T Materialize<T>(Dictionary<string, AttributeValue> item)
    {
        var entity = Activator.CreateInstance<T>();
        if (entity == null)
            return default!;

        var entityType = typeof(T);
        foreach (var property in entityType.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
                continue;

            if (!item.TryGetValue(property.Name, out var value))
                continue;

            var converted = ConvertAttributeValue(value, property.PropertyType);
            if (converted == null &&
                property.PropertyType.IsValueType &&
                Nullable.GetUnderlyingType(property.PropertyType) == null)
                continue;

            property.SetValue(entity, converted);
        }

        return entity;
    }

    private static object? ConvertAttributeValue(AttributeValue value, Type targetType)
    {
        if (value.NULL == true)
            return null;

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableType == typeof(string))
            return value.S;

        if (nonNullableType == typeof(bool))
            return value.BOOL;

        if (nonNullableType == typeof(int) &&
            int.TryParse(value.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        if (nonNullableType == typeof(long) &&
            long.TryParse(
                value.N,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var longValue))
            return longValue;

        if (nonNullableType == typeof(double) &&
            double.TryParse(
                value.N,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var doubleValue))
            return doubleValue;

        if (nonNullableType == typeof(decimal) &&
            decimal.TryParse(
                value.N,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var decimalValue))
            return decimalValue;

        if (nonNullableType == typeof(Guid) && Guid.TryParse(value.S, out var guidValue))
            return guidValue;

        if (nonNullableType == typeof(DateTime) &&
            DateTime.TryParse(
                value.S,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateValue))
            return dateValue;

        return null;
    }
}
