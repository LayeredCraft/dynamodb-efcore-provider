using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents access to a complex property attribute in a DynamoDB query expression.
///     Carries the <see cref="IComplexProperty" /> so the query pipeline can resolve the
///     DynamoDB attribute name without a model scan.
/// </summary>
/// <remarks>
///     Analogous to <c>DynamoObjectAccessExpression</c> (now removed) for owned references.
///     Used in <c>TranslateNestedMemberChain</c> for intermediate complex property path segments.
/// </remarks>
public sealed class DynamoComplexPropertyAccessExpression(IComplexProperty complexProperty)
    : SqlExpression(complexProperty.ClrType, null)
{
    /// <summary>The complex property this expression accesses.</summary>
    public IComplexProperty ComplexProperty { get; } = complexProperty;

    /// <summary>The DynamoDB attribute name used to store this complex property's map.</summary>
    public string AttributeName => ((IReadOnlyComplexProperty)ComplexProperty).GetAttributeName();

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping) => this;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append(AttributeName);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is DynamoComplexPropertyAccessExpression o
            && ComplexProperty.Name == o.ComplexProperty.Name
            && ComplexProperty.DeclaringType.ClrType == o.ComplexProperty.DeclaringType.ClrType;

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(ComplexProperty.Name, ComplexProperty.DeclaringType.ClrType);
}
