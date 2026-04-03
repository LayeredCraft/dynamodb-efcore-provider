using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;

namespace EntityFrameworkCore.DynamoDb.Update.Internal;

/// <summary>
///     Generates a PartiQL INSERT statement from an <see cref="InsertExpression" /> tree.
/// </summary>
/// <remarks>
///     This generator walks the expression tree produced by <see cref="InsertExpressionBuilder" />
///     and emits the corresponding PartiQL with positional <c>?</c> parameters. Value conversion
///     is owned by <see cref="DynamoTypeMapping" />.
/// </remarks>
public sealed class DynamoWriteSqlGenerator
{
    /// <summary>
    ///     Generates a <see cref="DynamoPartiQlQuery" /> from the given INSERT expression tree
    ///     and its runtime parameter values.
    /// </summary>
    /// <param name="insertExpression">The INSERT expression tree to walk.</param>
    /// <param name="parameterValues">
    ///     Runtime values keyed by the parameter names in the expression tree (e.g. <c>p0</c>,
    ///     <c>p1</c>). Produced by <see cref="InsertExpressionBuilder.Build" />.
    /// </param>
    /// <returns>
    ///     A <see cref="DynamoPartiQlQuery" /> containing the PartiQL SQL string and the ordered
    ///     list of <see cref="AttributeValue" /> parameters.
    /// </returns>
    public DynamoPartiQlQuery Generate(
        InsertExpression insertExpression,
        IReadOnlyDictionary<string, object?> parameterValues)
    {
        var sql = new StringBuilder();
        var parameters = new List<AttributeValue>();

        // INSERT INTO "TableName" VALUE {'Attr': ?, ...}
        sql.Append("INSERT INTO ");
        AppendIdentifier(sql, insertExpression.TableName);
        sql.Append(" VALUE {");

        var first = true;
        foreach (var field in insertExpression.Fields)
        {
            if (!first)
                sql.Append(", ");

            first = false;

            // DynamoDB PartiQL VALUE map keys are single-quoted string literals,
            // NOT double-quoted identifiers as used in WHERE clauses.
            sql.Append('\'');
            sql.Append(field.AttributeName);
            sql.Append("': ");

            AppendValue(sql, parameters, field.Value, parameterValues);
        }

        sql.Append('}');

        return new DynamoPartiQlQuery(sql.ToString(), parameters);
    }

    /// <summary>
    ///     Appends a positional <c>?</c> placeholder and resolves the <see cref="AttributeValue" />
    ///     for the given value expression.
    /// </summary>
    private static void AppendValue(
        StringBuilder sql,
        List<AttributeValue> parameters,
        SqlExpression valueExpression,
        IReadOnlyDictionary<string, object?> parameterValues)
    {
        switch (valueExpression)
        {
            case SqlParameterExpression param:
                parameterValues.TryGetValue(param.Name, out var value);
                sql.Append('?');
                parameters.Add(
                    // SaveChanges writes stay model-value-facing; the mapping owns converter
                    // composition and AttributeValue creation for both scalars and collections.
                    (param.TypeMapping as DynamoTypeMapping)?.CreateAttributeValue(value)
                    ?? throw new InvalidOperationException(
                        $"INSERT parameter '{param.Name}' requires a DynamoTypeMapping."));
                break;

            case SqlConstantExpression constant:
                sql.Append('?');
                parameters.Add(
                    (constant.TypeMapping as DynamoTypeMapping)?.CreateAttributeValue(
                        constant.Value)
                    ?? throw new InvalidOperationException(
                        "INSERT constant requires a DynamoTypeMapping."));
                break;

            default:
                throw new NotSupportedException(
                    $"Value expression type '{valueExpression.GetType().Name}' is not supported "
                    + "in INSERT statements.");
        }
    }

    /// <summary>Appends a double-quoted, escaped DynamoDB identifier.</summary>
    private static void AppendIdentifier(StringBuilder sql, string name)
    {
        sql.Append('"');
        sql.Append(name.Replace("\"", "\"\""));
        sql.Append('"');
    }
}
