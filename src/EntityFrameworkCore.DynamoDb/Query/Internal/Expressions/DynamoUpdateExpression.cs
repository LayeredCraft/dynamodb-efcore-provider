using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>A single SET assignment for <see cref="DynamoUpdateExpression" />.</summary>
public sealed class DynamoUpdateSetter
{
    /// <summary>Creates a new update setter.</summary>
    public DynamoUpdateSetter(
        IReadOnlyProperty property,
        string attributeNamePath,
        SqlExpression value,
        bool isSelfReferencing)
    {
        Property = property;
        AttributeNamePath = attributeNamePath;
        Value = value;
        IsSelfReferencing = isSelfReferencing;
    }

    /// <summary>The mapped property being assigned.</summary>
    public IReadOnlyProperty Property { get; }

    /// <summary>
    ///     The dotted DynamoDB attribute path assigned to. Top-level properties use the attribute
    ///     name; nested paths use <c>"parent"."child"</c>-style segments joined with dots.
    /// </summary>
    public string AttributeNamePath { get; }

    /// <summary>The assigned value expression.</summary>
    public SqlExpression Value { get; }

    /// <summary>
    ///     <see langword="true" /> when <see cref="Value" /> reads the property being assigned,
    ///     producing a self-referencing SET clause such as <c>n = n + 1</c>.
    /// </summary>
    public bool IsSelfReferencing { get; }

    /// <summary>Returns a copy with a new value when the value changed.</summary>
    public DynamoUpdateSetter Update(SqlExpression value)
        => value != Value
            ? new DynamoUpdateSetter(Property, AttributeNamePath, value, IsSelfReferencing)
            : this;
}

/// <summary>
///     Represents an <c>ExecuteUpdate</c> operation over a base-table item fully identified by
///     its primary key, producing a 0/1 affected-item count.
/// </summary>
public sealed class DynamoUpdateExpression : Expression, IPrintableExpression
{
    /// <summary>Creates a new update expression.</summary>
    public DynamoUpdateExpression(
        SelectExpression selectExpression,
        IEntityType entityType,
        IReadOnlyList<DynamoUpdateSetter> setters)
    {
        SelectExpression = selectExpression;
        EntityType = entityType;
        Setters = setters;
    }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type { get; } = typeof(int);

    /// <summary>The underlying select expression carrying the WHERE predicate.</summary>
    public SelectExpression SelectExpression { get; }

    /// <summary>The entity type being updated.</summary>
    public IEntityType EntityType { get; }

    /// <summary>The SET assignments.</summary>
    public IReadOnlyList<DynamoUpdateSetter> Setters { get; }

    /// <summary>Creates an updated update expression when children changed.</summary>
    public DynamoUpdateExpression Update(
        SelectExpression selectExpression,
        IReadOnlyList<DynamoUpdateSetter> setters)
    {
        if (selectExpression != SelectExpression)
            return new DynamoUpdateExpression(selectExpression, EntityType, setters);

        for (var i = 0; i < setters.Count; i++)
            if (!ReferenceEquals(setters[i], Setters[i]))
                return new DynamoUpdateExpression(selectExpression, EntityType, setters);

        return this;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var selectExpression = (SelectExpression)visitor.Visit(SelectExpression);

        var newSetters = new DynamoUpdateSetter[Setters.Count];
        var changed = !ReferenceEquals(selectExpression, SelectExpression);
        for (var i = 0; i < Setters.Count; i++)
        {
            var setter = Setters[i];
            var newValue = (SqlExpression)visitor.Visit(setter.Value);
            var newSetter = setter.Update(newValue);
            newSetters[i] = newSetter;
            changed |= !ReferenceEquals(newSetter, setter);
        }

        return changed
            ? new DynamoUpdateExpression(selectExpression, EntityType, newSetters!)
            : this;
    }

    /// <inheritdoc />
    public void Print(Microsoft.EntityFrameworkCore.Query.ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine($"DynamoUpdateExpression ({EntityType.DisplayName()}):");
        expressionPrinter.Append("SelectExpression: ");
        expressionPrinter.Visit(SelectExpression);
        expressionPrinter.AppendLine();
        foreach (var setter in Setters)
        {
            expressionPrinter.Append($"SET {setter.AttributeNamePath} = ");
            expressionPrinter.Visit(setter.Value);
            if (setter.IsSelfReferencing)
                expressionPrinter.Append(" [self-referencing]");

            expressionPrinter.AppendLine();
        }
    }
}

/// <summary>
///     Represents an <c>ExecuteDelete</c> operation over a base-table item fully identified by
///     its primary key, producing a 0/1 affected-item count.
/// </summary>
public sealed class DynamoDeleteExpression(
    SelectExpression selectExpression,
    IEntityType entityType) : Expression, IPrintableExpression
{
    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type { get; } = typeof(int);

    /// <summary>The underlying select expression carrying the WHERE predicate.</summary>
    public SelectExpression SelectExpression { get; } = selectExpression;

    /// <summary>The entity type being deleted.</summary>
    public IEntityType EntityType { get; } = entityType;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var selectExpression = (SelectExpression)visitor.Visit(SelectExpression);
        return selectExpression != SelectExpression
            ? new DynamoDeleteExpression(selectExpression, EntityType)
            : this;
    }

    /// <inheritdoc />
    public void Print(Microsoft.EntityFrameworkCore.Query.ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine($"DynamoDeleteExpression ({EntityType.DisplayName()}):");
        expressionPrinter.Append("SelectExpression: ");
        expressionPrinter.Visit(SelectExpression);
    }
}
