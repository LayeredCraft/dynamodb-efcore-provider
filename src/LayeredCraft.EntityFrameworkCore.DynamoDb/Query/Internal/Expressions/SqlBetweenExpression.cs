using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a PartiQL <c>subject BETWEEN low AND high</c> range predicate.</summary>
/// <remarks>
///     Both bounds are inclusive. This expression is produced by rewriting
///     <c>(prop &gt;= low) AND (prop &lt;= high)</c> patterns during predicate normalization.
/// </remarks>
public sealed class SqlBetweenExpression : SqlExpression
{
    /// <summary>Initializes a new BETWEEN predicate expression.</summary>
    /// <param name="subject">The attribute expression whose value is tested.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    public SqlBetweenExpression(SqlExpression subject, SqlExpression low, SqlExpression high) :
        base(typeof(bool), null)
    {
        Subject = subject;
        Low = low;
        High = high;
    }

    private SqlBetweenExpression(
        SqlExpression subject,
        SqlExpression low,
        SqlExpression high,
        CoreTypeMapping? typeMapping) : base(typeof(bool), typeMapping)
    {
        Subject = subject;
        Low = low;
        High = high;
    }

    /// <summary>The attribute expression whose value is tested against the range.</summary>
    public SqlExpression Subject { get; }

    /// <summary>The inclusive lower bound of the range.</summary>
    public SqlExpression Low { get; }

    /// <summary>The inclusive upper bound of the range.</summary>
    public SqlExpression High { get; }

    /// <summary>
    ///     Creates a new expression with updated operands, returning <see langword="this" /> if all
    ///     operands are unchanged.
    /// </summary>
    /// <param name="subject">The new subject expression.</param>
    /// <param name="low">The new lower bound expression.</param>
    /// <param name="high">The new upper bound expression.</param>
    public SqlBetweenExpression Update(SqlExpression subject, SqlExpression low, SqlExpression high)
        => subject != Subject || low != Low || high != High
            ? new SqlBetweenExpression(subject, low, high, TypeMapping)
            : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlBetweenExpression(Subject, Low, High, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Subject);
        expressionPrinter.Append(" BETWEEN ");
        expressionPrinter.Visit(Low);
        expressionPrinter.Append(" AND ");
        expressionPrinter.Visit(High);
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlBetweenExpression between
            && base.Equals(between)
            && Subject.Equals(between.Subject)
            && Low.Equals(between.Low)
            && High.Equals(between.High);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Subject, Low, High);
}
