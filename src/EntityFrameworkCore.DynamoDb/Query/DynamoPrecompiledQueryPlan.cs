using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.Query;

/// <summary>Recreates a translated DynamoDB query for an EF Core generated query interceptor.</summary>
public static class DynamoPrecompiledQueryPlan
{
    /// <summary>Creates a serializable representation of a translated query.</summary>
    public static string Serialize(SelectExpression selectExpression)
        => JsonSerializer.Serialize(
            new Plan(
                selectExpression.TableName,
                selectExpression.QueryEntityTypeName,
                selectExpression.IndexName,
                selectExpression
                    .Projection
                    .Select(static projection => Create(projection.Expression))
                    .ToArray(),
                selectExpression.Predicate is null ? null : Create(selectExpression.Predicate),
                selectExpression
                    .Orderings
                    .Select(static ordering
                        => new OrderingPlan(Create(ordering.Expression), ordering.IsAscending))
                    .ToArray()));

    /// <summary>Recreates a translated query from a generated interceptor's literal query plan.</summary>
#pragma warning disable EF9100
    public static SelectExpression Create(
        MaterializerLiftableConstantContext context,
        string serializedPlan)
    {
        var plan = JsonSerializer.Deserialize<Plan>(serializedPlan)
            ?? throw new InvalidOperationException("The generated DynamoDB query plan is empty.");

        var typeMappings = new TypeMappings(
            context.Dependencies.Model,
            context.Dependencies.TypeMappingSource,
            plan.QueryEntityTypeName);
        var selectExpression = new SelectExpression(plan.TableName, plan.QueryEntityTypeName);
        selectExpression.ApplyIndexName(plan.IndexName);

        foreach (var projection in plan.Projection)
            selectExpression.AddToProjection(Create(projection, typeMappings), "");

        if (plan.Predicate is not null)
            selectExpression.ApplyPredicate(Create(plan.Predicate, typeMappings));

        foreach (var ordering in plan.Orderings)
            selectExpression.AppendOrdering(
                new OrderingExpression(
                    Create(ordering.Expression, typeMappings),
                    ordering.IsAscending));

        return selectExpression;
    }

    /// <summary>Resolves the provider SQL generator for a generated query interceptor.</summary>
    public static IDynamoQuerySqlGeneratorFactory GetSqlGeneratorFactory(
        MaterializerLiftableConstantContext context)
        => context.Dependencies.ContextServices.InternalServiceProvider
            .GetRequiredService<IDynamoQuerySqlGeneratorFactory>();
#pragma warning restore EF9100

    private static ExpressionPlan Create(SqlExpression expression)
        => expression switch
        {
            SqlPropertyExpression property => new ExpressionPlan(
                "property",
                property.PropertyName,
                TypeName(property.Type),
                property.IsPartitionKey),
            SqlParameterExpression parameter => new ExpressionPlan(
                "parameter",
                parameter.Name,
                TypeName(parameter.Type)),
            SqlConstantExpression constant => new ExpressionPlan(
                "constant",
                SerializeConstant(constant.Value),
                TypeName(constant.Type)),
            SqlBinaryExpression binary => new ExpressionPlan(
                "binary",
                ((int)binary.OperatorType).ToString(CultureInfo.InvariantCulture),
                TypeName(binary.Type),
                Left: Create(binary.Left),
                Right: Create(binary.Right)),
            SqlFunctionExpression function => new ExpressionPlan(
                "function",
                function.Name,
                TypeName(function.Type),
                Arguments: function.Arguments.Select(Create).ToArray()),
            SqlIsNullExpression isNull => new ExpressionPlan(
                "is-null",
                ((int)isNull.Operator).ToString(CultureInfo.InvariantCulture),
                Operand: Create(isNull.Operand)),
            SqlBetweenExpression between => new ExpressionPlan(
                "between",
                Operand: Create(between.Subject),
                Left: Create(between.Low),
                Right: Create(between.High)),
            SqlInExpression @in => new ExpressionPlan(
                "in",
                IsPartitionKey: @in.IsPartitionKeyComparison,
                Operand: Create(@in.Item),
                Arguments: @in.Values?.Select(Create).ToArray(),
                ValuesParameter: @in.ValuesParameter is null ? null : Create(@in.ValuesParameter)),
            SqlParenthesizedExpression parenthesized => new ExpressionPlan(
                "parenthesized",
                Operand: Create(parenthesized.Operand)),
            SqlUnaryExpression unary => new ExpressionPlan(
                "unary",
                ((int)unary.OperatorType).ToString(CultureInfo.InvariantCulture),
                Operand: Create(unary.Operand)),
            DynamoScalarAccessExpression scalar => new ExpressionPlan(
                "scalar",
                scalar.PropertyName,
                TypeName(scalar.Type),
                Operand: Create((SqlExpression)scalar.Parent)),
            DynamoListIndexExpression listIndex => new ExpressionPlan(
                "list-index",
                listIndex.Index.ToString(CultureInfo.InvariantCulture),
                TypeName(listIndex.Type),
                Operand: Create((SqlExpression)listIndex.Source)),
            _ => throw new NotSupportedException(
                $"DynamoDB precompiled queries do not support {expression.GetType().Name} yet.")
        };

    private static SqlExpression Create(ExpressionPlan expression, TypeMappings mappings)
    {
        var type = ResolveType(expression.TypeName);

        return expression.Kind switch
        {
            "property" => new SqlPropertyExpression(
                expression.Value!,
                type,
                mappings.Find(type, expression.Value),
                expression.IsPartitionKey),
            "parameter" => new SqlParameterExpression(expression.Value!, type, mappings.Find(type)),
            "constant" => new SqlConstantExpression(
                DeserializeConstant(expression.Value, type),
                type,
                mappings.Find(type)),
            "binary" => new SqlBinaryExpression(
                (ExpressionType)int.Parse(expression.Value!, CultureInfo.InvariantCulture),
                Create(expression.Left!, mappings),
                Create(expression.Right!, mappings),
                type,
                mappings.Find(type)),
            "function" => new SqlFunctionExpression(
                expression.Value!,
                expression.Arguments!.Select(argument => Create(argument, mappings)).ToArray(),
                type,
                mappings.Find(type)),
            "is-null" => new SqlIsNullExpression(
                Create(expression.Operand!, mappings),
                (IsNullOperator)int.Parse(expression.Value!, CultureInfo.InvariantCulture)),
            "between" => new SqlBetweenExpression(
                Create(expression.Operand!, mappings),
                Create(expression.Left!, mappings),
                Create(expression.Right!, mappings)),
            "in" => new SqlInExpression(
                Create(expression.Operand!, mappings),
                expression.Arguments?.Select(argument => Create(argument, mappings)).ToArray(),
                expression.ValuesParameter is null
                    ? null
                    : (SqlParameterExpression)Create(expression.ValuesParameter, mappings),
                expression.IsPartitionKey,
                mappings.Find(typeof(bool))),
            "parenthesized" =>
                new SqlParenthesizedExpression(Create(expression.Operand!, mappings)),
            "unary" => new SqlUnaryExpression(
                (ExpressionType)int.Parse(expression.Value!, CultureInfo.InvariantCulture),
                Create(expression.Operand!, mappings)),
            "scalar" => new DynamoScalarAccessExpression(
                Create(expression.Operand!, mappings),
                expression.Value!,
                type,
                mappings.Find(type)),
            "list-index" => new DynamoListIndexExpression(
                Create(expression.Operand!, mappings),
                int.Parse(expression.Value!, CultureInfo.InvariantCulture),
                type,
                mappings.Find(type)),
            _ => throw new InvalidOperationException(
                $"Unknown DynamoDB query plan node '{expression.Kind}'.")
        };
    }

    private static string TypeName(Type type)
        => type.AssemblyQualifiedName
            ?? throw new InvalidOperationException($"Unable to serialize type '{type}'.");

    private static Type ResolveType(string? typeName)
        => Type.GetType(
            typeName ?? throw new InvalidOperationException("A query plan type is missing."),
            throwOnError: true)!;

    private static string? SerializeConstant(object? value)
        => value is null ? null : JsonSerializer.Serialize(value, value.GetType());

    private static object? DeserializeConstant(string? value, Type type)
        => value is null ? null : JsonSerializer.Deserialize(value, type);

    private sealed record Plan(
        string TableName,
        string? QueryEntityTypeName,
        string? IndexName,
        ExpressionPlan[] Projection,
        ExpressionPlan? Predicate,
        OrderingPlan[] Orderings);

    private sealed record OrderingPlan(ExpressionPlan Expression, bool IsAscending);

    private sealed record ExpressionPlan(
        string Kind,
        string? Value = null,
        string? TypeName = null,
        bool IsPartitionKey = false,
        ExpressionPlan? Operand = null,
        ExpressionPlan? Left = null,
        ExpressionPlan? Right = null,
        ExpressionPlan[]? Arguments = null,
        ExpressionPlan? ValuesParameter = null);

    private sealed class TypeMappings(
        IModel model,
        ITypeMappingSource typeMappingSource,
        string? entityTypeName)
    {
        private readonly IEntityType? _entityType = entityTypeName is null
            ? null
            : model.FindEntityType(entityTypeName);

        public CoreTypeMapping? Find(Type type, string? attributeName = null)
        {
            if (attributeName is not null && _entityType is not null)
            {
                var property =
                    _entityType
                        .GetProperties()
                        .FirstOrDefault(property
                            => property.ClrType == type
                            && property.GetAttributeName() == attributeName);
                if (property is not null)
                    return property.GetTypeMapping();
            }

            return typeMappingSource.FindMapping(type);
        }
    }
}
