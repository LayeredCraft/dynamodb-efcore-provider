using System.Collections;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Update.Internal;

/// <summary>
///     Translates an <see cref="IUpdateEntry" /> for an <see cref="EntityState.Added" /> entity
///     into an <see cref="InsertExpression" /> tree plus a runtime parameter-values dictionary.
/// </summary>
/// <remarks>
///     Only scalar (non-owned-navigation) properties are included. The expression tree contains no
///     raw CLR values — all values are represented as <see cref="SqlParameterExpression" /> nodes
///     whose runtime values are stored separately in <see cref="InsertStatementPlan.ParameterValues" />.
/// </remarks>
public sealed class InsertExpressionBuilder(ITypeMappingSource typeMappingSource)
{
    /// <summary>
    ///     Builds an <see cref="InsertStatementPlan" /> for the given Added entity entry.
    /// </summary>
    /// <param name="entry">
    ///     An <see cref="IUpdateEntry" /> whose <see cref="IUpdateEntry.EntityState" /> is
    ///     <see cref="EntityState.Added" />. Owned entity entries are not supported and should be
    ///     filtered out by the caller.
    /// </param>
    /// <returns>
    ///     An <see cref="InsertStatementPlan" /> containing the expression tree and parameter values.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the entity type has no mapped table name, or when the discriminator
    ///     shadow property has a null value at the time of the build.
    /// </exception>
    public InsertStatementPlan Build(IUpdateEntry entry)
    {
        var entityType = entry.EntityType;

        // Walk the base-type chain to find the entity that owns the table-name annotation
        var tableName = entityType.GetTableGroupName()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' has no mapped DynamoDB table name.");

        var fields = new List<InsertFieldExpression>();
        var parameterValues = new Dictionary<string, object?>();
        var paramIndex = 0;

        // Identify the discriminator property so we can guard against a null discriminator value
        var discriminatorProperty = entityType.FindDiscriminatorProperty();

        foreach (var property in entityType.GetProperties())
        {
            // Primitive collection properties (List<T>, HashSet<T>, Dictionary<TK,TV>, etc.)
            // require map/list/set AttributeValue serialization, which is not yet implemented
            // (planned for story 6gu.2). Throw rather than silently dropping the data.
            if (property.ClrType != typeof(string)
                && typeof(IEnumerable).IsAssignableFrom(property.ClrType))
                throw new NotSupportedException(
                    $"Property '{property.Name}' on entity type '{entityType.Name}' is a primitive "
                    + $"collection ('{property.ClrType.Name}'). Primitive collection serialization "
                    + "is not yet supported. Remove or exclude the property until support is added.");

            var paramName = $"p{paramIndex++}";
            var typeMapping = typeMappingSource.FindMapping(property) as CoreTypeMapping;
            var currentValue = entry.GetCurrentValue(property);

            // The discriminator shadow property must be populated by EF Core's conventions
            // before SaveChanges is called. A null discriminator would write invalid data.
            if (property == discriminatorProperty && currentValue is null)
                throw new InvalidOperationException(
                    $"Discriminator property '{property.Name}' has no value on entity type "
                    + $"'{entityType.Name}'. Ensure the entity was added via DbSet.Add() so "
                    + "EF Core can populate the discriminator value.");

            parameterValues[paramName] = currentValue;

            fields.Add(
                new InsertFieldExpression(
                    property.GetAttributeName(),
                    new SqlParameterExpression(paramName, property.ClrType, typeMapping)));
        }

        return new InsertStatementPlan(new InsertExpression(tableName, fields), parameterValues);
    }
}
