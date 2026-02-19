using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents access to an embedded DynamoDB list attribute for an owned navigation.</summary>
public sealed class DynamoObjectArrayProjectionExpression : Expression
{
    /// <summary>Creates an embedded list projection expression for an owned collection navigation.</summary>
    public DynamoObjectArrayProjectionExpression(INavigation navigation, string attributeName)
    {
        Navigation = navigation;
        AttributeName = attributeName;
    }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type
        => typeof(IEnumerable<>).MakeGenericType(Navigation.TargetEntityType.ClrType);

    /// <summary>The owned collection navigation represented by this projection.</summary>
    public INavigation Navigation { get; }

    /// <summary>The containing DynamoDB attribute name storing the owned collection list.</summary>
    public string AttributeName { get; }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj != null
            && (ReferenceEquals(this, obj)
                || (obj is DynamoObjectArrayProjectionExpression other && Equals(other)));

    private bool Equals(DynamoObjectArrayProjectionExpression other)
        => Navigation.Name == other.Navigation.Name
            && Navigation.TargetEntityType.ClrType == other.Navigation.TargetEntityType.ClrType
            && AttributeName == other.AttributeName;

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Navigation.Name, Navigation.TargetEntityType.ClrType, AttributeName);
}
