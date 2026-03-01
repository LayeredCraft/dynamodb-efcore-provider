using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents an IN predicate in a SQL expression.</summary>
public class SqlInExpression(
    SqlExpression item,
    IReadOnlyList<SqlExpression>? values,
    SqlParameterExpression? valuesParameter,
    bool isPartitionKeyComparison,
    CoreTypeMapping? typeMapping) : SqlExpression(typeof(bool), typeMapping)
{
    /// <summary>The expression whose value is checked for membership.</summary>
    public SqlExpression Item { get; } = item;

    /// <summary>The inline values used by this IN predicate, when present.</summary>
    public IReadOnlyList<SqlExpression>? Values { get; } = values;

    /// <summary>The collection parameter used by this IN predicate, when present.</summary>
    public SqlParameterExpression? ValuesParameter { get; } = valuesParameter;

    /// <summary>Indicates whether the compared item is the partition key property.</summary>
    public bool IsPartitionKeyComparison { get; } = isPartitionKeyComparison;

    /// <summary>Creates a new IN expression with updated children.</summary>
    public SqlInExpression Update(
        SqlExpression item,
        IReadOnlyList<SqlExpression>? values,
        SqlParameterExpression? valuesParameter)
        => item != Item || !ReferenceEquals(values, Values) || valuesParameter != ValuesParameter
            ? new SqlInExpression(
                item,
                values,
                valuesParameter,
                IsPartitionKeyComparison,
                TypeMapping)
            : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlInExpression(
            Item,
            Values,
            ValuesParameter,
            IsPartitionKeyComparison,
            typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Item);
        expressionPrinter.Append(" IN [");

        if (Values != null)
            for (var i = 0; i < Values.Count; i++)
            {
                if (i > 0)
                    expressionPrinter.Append(", ");

                expressionPrinter.Visit(Values[i]);
            }
        else if (ValuesParameter != null)
            expressionPrinter.Visit(ValuesParameter);

        expressionPrinter.Append("]");
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlInExpression inExpression
            && base.Equals(inExpression)
            && Item.Equals(inExpression.Item)
            && Equals(ValuesParameter, inExpression.ValuesParameter)
            && EqualsInlineValues(inExpression.Values)
            && IsPartitionKeyComparison == inExpression.IsPartitionKeyComparison;

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(
            base.GetHashCode(),
            Item,
            ValuesParameter,
            IsPartitionKeyComparison);

        if (Values != null)
            foreach (var value in Values)
                hash = HashCode.Combine(hash, value);

        return hash;
    }

    /// <summary>Validates the IN expression shape to ensure exactly one value source is set.</summary>
    public static void ValidateValueSource(
        IReadOnlyList<SqlExpression>? values,
        SqlParameterExpression? valuesParameter)
    {
        var hasValues = values != null;
        var hasValuesParameter = valuesParameter != null;
        if (hasValues == hasValuesParameter)
            throw new ArgumentException(
                "An IN expression must specify either inline values or a values parameter.");
    }

    /// <summary>Returns whether the inline values equal another inline value list.</summary>
    private bool EqualsInlineValues(IReadOnlyList<SqlExpression>? otherValues)
    {
        if (Values == null && otherValues == null)
            return true;

        if (Values == null || otherValues == null || Values.Count != otherValues.Count)
            return false;

        for (var i = 0; i < Values.Count; i++)
            if (!Values[i].Equals(otherValues[i]))
                return false;

        return true;
    }
}
