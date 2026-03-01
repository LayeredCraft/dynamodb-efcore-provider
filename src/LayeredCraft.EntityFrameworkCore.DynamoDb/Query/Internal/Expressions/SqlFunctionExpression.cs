using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a function invocation in a SQL expression.</summary>
public class SqlFunctionExpression(
    string name,
    IReadOnlyList<SqlExpression> arguments,
    Type type,
    CoreTypeMapping? typeMapping) : SqlExpression(type, typeMapping)
{
    /// <summary>The function name.</summary>
    public string Name { get; } = name;

    /// <summary>The function arguments.</summary>
    public IReadOnlyList<SqlExpression> Arguments { get; } = arguments;

    /// <summary>Creates a new function expression with updated arguments.</summary>
    public SqlFunctionExpression Update(IReadOnlyList<SqlExpression> arguments)
        => !ReferenceEquals(arguments, Arguments)
            ? new SqlFunctionExpression(Name, arguments, Type, TypeMapping)
            : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlFunctionExpression(Name, Arguments, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append(Name);
        expressionPrinter.Append("(");
        for (var i = 0; i < Arguments.Count; i++)
        {
            if (i > 0)
                expressionPrinter.Append(", ");

            expressionPrinter.Visit(Arguments[i]);
        }

        expressionPrinter.Append(")");
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlFunctionExpression functionExpression
            && base.Equals(functionExpression)
            && Name == functionExpression.Name
            && Arguments.SequenceEqual(functionExpression.Arguments);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(base.GetHashCode(), Name);
        foreach (var argument in Arguments)
            hash = HashCode.Combine(hash, argument);

        return hash;
    }
}
