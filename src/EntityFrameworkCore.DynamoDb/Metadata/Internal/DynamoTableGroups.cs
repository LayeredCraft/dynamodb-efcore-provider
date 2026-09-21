using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>
///     A DynamoDB table as the model understands it: the entity types that share one table, and the
///     logical name declared for it (independent of the physical name).
/// </summary>
internal sealed class DynamoTableGroup(
    string modelName,
    string? logicalName,
    IReadOnlyList<IReadOnlyEntityType> entityTypes)
{
    /// <summary>The physical table name configured in the model (the design-time name).</summary>
    public string ModelName { get; } = modelName;

    /// <summary>The logical table name declared on any entity type of the table, if any.</summary>
    public string? LogicalName { get; } = logicalName;

    /// <summary>All entity types (roots and derived types) mapped to the table.</summary>
    public IReadOnlyList<IReadOnlyEntityType> EntityTypes { get; } = entityTypes;
}

/// <summary>Resolves table groups and their logical names from a model.</summary>
internal static class DynamoTableGroups
{
    /// <summary>
    ///     Groups entity types by their configured table and resolves each group's logical name.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Entity types of one table declare different logical names, or one logical name is declared
    ///     by more than one table.
    /// </exception>
    public static IReadOnlyList<DynamoTableGroup> Resolve(IReadOnlyModel model)
    {
        List<DynamoTableGroup> groups = [];

        foreach (var table in model
            .GetEntityTypes()
            .GroupBy(static entityType => entityType.ComputeTableGroupName(), StringComparer.Ordinal))
        {
            var entityTypes = table.ToArray();
            var distinct = DeclaredLogicalName(table.Key, entityTypes);

            groups.Add(new DynamoTableGroup(table.Key, distinct, entityTypes));
        }

        foreach (var duplicate in groups
            .Where(static group => group.LogicalName is not null)
            .GroupBy(static group => group.LogicalName!, StringComparer.Ordinal)
            .Where(static logical => logical.Count() > 1))
            throw new InvalidOperationException(
                $"The logical table name '{duplicate.Key}' is declared by more than one DynamoDB table: "
                + string.Join(", ", duplicate.Select(static group => $"'{group.ModelName}'"))
                + ". A logical table name identifies exactly one table.");

        return groups;
    }

    /// <summary>
    ///     Resolves the logical table identity of the table an entity type is mapped to: the identity
    ///     declared on any entity type of that table, or <see langword="null" /> when none is declared.
    /// </summary>
    /// <remarks>
    ///     Reads annotations only, so it is safe on a partially built model. It scans the model's entity
    ///     types once per call and is not intended for query hot paths.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Entity types of the table declare different logical names.
    /// </exception>
    public static string? ResolveLogicalTableName(IReadOnlyEntityType entityType)
    {
        var tableName = entityType.ComputeTableGroupName();
        return DeclaredLogicalName(
            tableName,
            entityType
                .Model
                .GetEntityTypes()
                .Where(other => string.Equals(
                    other.ComputeTableGroupName(),
                    tableName,
                    StringComparison.Ordinal))
                .ToArray());
    }

    private static string? DeclaredLogicalName(
        string tableName,
        IReadOnlyList<IReadOnlyEntityType> tableEntityTypes)
    {
        var declarations = tableEntityTypes
            .Select(static entityType => (
                EntityType: entityType,
                Name: entityType.FindAnnotation(DynamoAnnotationNames.LogicalTableName)?.Value
                    as string))
            .Where(static declaration => declaration.Name is not null)
            .ToArray();

        var distinct = declarations
            .Select(static declaration => declaration.Name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinct.Length > 1)
            throw new InvalidOperationException(
                $"The entity types mapped to DynamoDB table '{tableName}' declare conflicting logical table names: "
                + string.Join(
                    ", ",
                    declarations.Select(static declaration
                        => $"'{declaration.Name}' on '{declaration.EntityType.DisplayName()}'"))
                + ". A table has one logical name; declare it consistently or on a single entity type.");

        return distinct.FirstOrDefault();
    }
}

