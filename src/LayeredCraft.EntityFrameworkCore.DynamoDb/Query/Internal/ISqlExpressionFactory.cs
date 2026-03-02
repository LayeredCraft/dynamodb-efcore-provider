using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory for creating SQL expressions with proper type mappings.
/// </summary>
public interface ISqlExpressionFactory
{
    /// <summary>
    /// Creates a SQL binary expression.
    /// </summary>
    SqlBinaryExpression? Binary(
        ExpressionType operatorType,
        SqlExpression left,
        SqlExpression right);

    /// <summary>
    /// Creates a SQL constant expression.
    /// </summary>
    SqlConstantExpression Constant(object? value, Type? type = null);

    /// <summary>
    /// Creates a SQL parameter expression.
    /// </summary>
    SqlParameterExpression Parameter(string name, Type type);

    /// <summary>
    /// Creates a SQL property expression.
    /// </summary>
    SqlPropertyExpression Property(string propertyName, Type type, bool isPartitionKey = false);

    /// <summary>Creates an IN expression using inline values.</summary>
    SqlInExpression In(
        SqlExpression item,
        IReadOnlyList<SqlExpression> values,
        bool isPartitionKeyComparison = false);

    /// <summary>Creates an IN expression using a collection parameter.</summary>
    SqlInExpression In(
        SqlExpression item,
        SqlParameterExpression valuesParameter,
        bool isPartitionKeyComparison = false);

    /// <summary>Creates a SQL function expression.</summary>
    SqlFunctionExpression Function(
        string name,
        IReadOnlyList<SqlExpression> arguments,
        Type returnType);

    /// <summary>
    /// Applies a type mapping to an existing SQL expression.
    /// </summary>
    SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, Type type);

    /// <summary>Creates a SQL NOT expression that negates the given boolean operand.</summary>
    SqlUnaryExpression Not(SqlExpression operand);

    /// <summary>
    /// Applies a default type mapping based on the expression's CLR type.
    /// </summary>
    SqlExpression ApplyDefaultTypeMapping(SqlExpression sqlExpression);
}
