using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoPartiqlStatementFactory(
    DynamoEntityItemSerializerSource serializerSource)
{
    /// <summary>Builds a PartiQL UPDATE statement for a modified tracked entity.</summary>
    internal (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedUpdateStatement(IUpdateEntry entry)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;

        var setClauses = new List<string>();
        var setParameters = new List<AttributeValue>();
        var removeClauses = new List<string>();
        var scalarAssignments = new List<(string Clause, AttributeValue Parameter)>();
        var nonScalarAssignments = new List<(string Clause, AttributeValue Parameter)>();
        var publicEntry = rootEntry.Context.Entry(rootEntry.Entity);

        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property))
                continue;

            if (property.IsPrimaryKey())
                throw new NotSupportedException(
                    $"SaveChanges Modified path does not support key mutation for "
                    + $"'{entityType.DisplayName()}.{property.Name}'.");

            var clause = $"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?";
            var parameter = serializerSource.SerializeProperty(entry, property);

            if (IsScalarModifiedProperty(property))
                scalarAssignments.Add((clause, parameter));
            else
                nonScalarAssignments.Add((clause, parameter));
        }

        foreach (var assignment in scalarAssignments)
        {
            setClauses.Add(assignment.Clause);
            setParameters.Add(assignment.Parameter);
        }

        foreach (var assignment in nonScalarAssignments)
        {
            setClauses.Add(assignment.Clause);
            setParameters.Add(assignment.Parameter);
        }

        // Complex properties are fully replaced when any of their nested scalars changed.
        foreach (var cp in entityType.GetComplexProperties())
            if (cp.IsCollection)
            {
                AppendComplexCollectionMutations(
                    publicEntry.ComplexCollection(cp),
                    cp,
                    $"\"{EscapeIdentifier(((IReadOnlyComplexProperty)cp).GetAttributeName())}\"",
                    setClauses,
                    setParameters,
                    removeClauses);
            }
            else
            {
                AppendComplexPropertyMutations(
                    publicEntry.ComplexProperty(cp),
                    cp,
                    $"\"{EscapeIdentifier(((IReadOnlyComplexProperty)cp).GetAttributeName())}\"",
                    setClauses,
                    setParameters,
                    removeClauses);
            }

        return FinalizeUpdateStatement(
            entry,
            entityType,
            tableName,
            setClauses,
            removeClauses,
            setParameters);
    }

    /// <summary>Builds a PartiQL DELETE statement for a deleted tracked entity.</summary>
    internal (string tableName, string sql, List<AttributeValue> parameters) BuildDeleteStatement(
        IUpdateEntry entry)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;

        if (tableName.Contains('"'))
            throw new ArgumentException(
                $"Table name '{tableName}' contains an illegal character ('\"'). "
                + "DynamoDB table names must not contain double-quote characters.",
                nameof(tableName));

        var entityType = entry.EntityType;

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var parameters = new List<AttributeValue>
        {
            serializerSource.GetOrBuildOriginalValueSerializer(partitionKeyProperty)(entry),
        };

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine($"DELETE FROM \"{EscapeIdentifier(tableName)}\"");
        sqlBuilder.Append(
            $"WHERE \"{EscapeIdentifier(partitionKeyProperty.GetAttributeName())}\" = ?");

        var sortKeyProperty = entityType.GetSortKeyProperty();
        if (sortKeyProperty is not null)
        {
            parameters.Add(
                serializerSource.GetOrBuildOriginalValueSerializer(sortKeyProperty)(entry));
            sqlBuilder.Append(
                $" AND \"{EscapeIdentifier(sortKeyProperty.GetAttributeName())}\" = ?");
        }

        AppendConcurrencyTokenPredicates(entry, entityType, sqlBuilder, parameters);

        return (tableName, sqlBuilder.ToString(), parameters);
    }

    /// <summary>Builds a PartiQL INSERT statement for a serialized DynamoDB item.</summary>
    internal (string sql, List<AttributeValue> parameters) BuildInsertStatement(
        string tableName,
        Dictionary<string, AttributeValue> item)
    {
        if (tableName.Contains('"'))
            throw new ArgumentException(
                $"Table name '{tableName}' contains an illegal character ('\"'). "
                + "DynamoDB table names must not contain double-quote characters.",
                nameof(tableName));

        var sql = new StringBuilder();
        sql.AppendLine($"INSERT INTO \"{tableName}\"");
        sql.Append("VALUE {");

        var parameters = new List<AttributeValue>(item.Count);
        var first = true;

        foreach (var (key, value) in item)
        {
            if (!first)
                sql.Append(", ");

            sql.Append($"'{EscapeStringLiteral(key)}': ?");
            parameters.Add(value);
            first = false;
        }

        sql.Append('}');
        return (sql.ToString(), parameters);
    }

    private (string tableName, string sql, List<AttributeValue> parameters)?
        FinalizeUpdateStatement(
            IUpdateEntry entry,
            IEntityType entityType,
            string tableName,
            List<string> setClauses,
            List<string> removeClauses,
            List<AttributeValue> setParameters)
    {
        if (setClauses.Count == 0 && removeClauses.Count == 0)
            return null;

        var whereParameters = new List<AttributeValue>();

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        whereParameters.Add(
            serializerSource.GetOrBuildOriginalValueSerializer(partitionKeyProperty)(entry));

        var whereClauses = new List<string>
        {
            $"\"{EscapeIdentifier(partitionKeyProperty.GetAttributeName())}\" = ?",
        };

        var sortKeyProperty = entityType.GetSortKeyProperty();
        if (sortKeyProperty is not null)
        {
            whereParameters.Add(
                serializerSource.GetOrBuildOriginalValueSerializer(sortKeyProperty)(entry));
            whereClauses.Add($"\"{EscapeIdentifier(sortKeyProperty.GetAttributeName())}\" = ?");
        }

        AppendConcurrencyTokenPredicates(entry, entityType, whereClauses, whereParameters);

        var sqlBuilder =
            new StringBuilder().AppendLine($"UPDATE \"{EscapeIdentifier(tableName)}\"");

        if (setClauses.Count > 0)
            sqlBuilder.AppendLine($"SET {string.Join(", ", setClauses)}");

        if (removeClauses.Count > 0)
            sqlBuilder.AppendLine($"REMOVE {string.Join(", ", removeClauses)}");

        sqlBuilder.Append($"WHERE {string.Join(" AND ", whereClauses)}");

        var parameters = new List<AttributeValue>(setParameters.Count + whereParameters.Count);
        parameters.AddRange(setParameters);
        parameters.AddRange(whereParameters);

        return (tableName, sqlBuilder.ToString(), parameters);
    }

    /// <summary>
    ///     Appends the UPDATE clauses required to persist a top-level or nested complex property.
    ///     Complex references use nested-path SET/REMOVE clauses where possible, while complex
    ///     collections are always replaced atomically when any tracked child entry changes.
    /// </summary>
    private void AppendComplexPropertyMutations(
        ComplexPropertyEntry complexEntry,
        IComplexProperty complexProperty,
        string path,
        List<string> setClauses,
        List<AttributeValue> setParameters,
        List<string> removeClauses)
    {
        if (!complexEntry.IsModified)
            return;

        var currentValue = complexEntry.CurrentValue;
        if (currentValue is null)
        {
            removeClauses.Add(path);
            return;
        }

        if (ShouldReplaceWholeComplexProperty(complexEntry, complexProperty))
        {
            setClauses.Add($"{path} = ?");
            setParameters.Add(EntityWritePlan.SerializeComplexProperty(currentValue, complexProperty));
            return;
        }

        foreach (var property in complexProperty.ComplexType.GetProperties())
        {
            if (property.IsRuntimeOnly())
                continue;

            var propertyEntry = complexEntry.Property(property);
            if (!propertyEntry.IsModified)
                continue;

            setClauses.Add($"{path}.\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            setParameters.Add(
                EntityWritePlan.SerializeScalarPropertyValue(propertyEntry.CurrentValue, property));
        }

        foreach (var nestedComplexProperty in complexProperty.ComplexType.GetComplexProperties())
        {
            var nestedPath =
                $"{path}.\"{EscapeIdentifier(((IReadOnlyComplexProperty)nestedComplexProperty).GetAttributeName())}\"";

            if (nestedComplexProperty.IsCollection)
            {
                AppendComplexCollectionMutations(
                    complexEntry.ComplexCollection(nestedComplexProperty),
                    nestedComplexProperty,
                    nestedPath,
                    setClauses,
                    setParameters,
                    removeClauses);
            }
            else
            {
                AppendComplexPropertyMutations(
                    complexEntry.ComplexProperty(nestedComplexProperty),
                    nestedComplexProperty,
                    nestedPath,
                    setClauses,
                    setParameters,
                    removeClauses);
            }
        }
    }

    /// <summary>
    ///     Appends the UPDATE clauses for a complex collection. Any tracked mutation within the
    ///     collection replaces the whole list attribute; null removes the attribute entirely.
    /// </summary>
    private static void AppendComplexCollectionMutations(
        ComplexCollectionEntry complexCollectionEntry,
        IComplexProperty complexProperty,
        string path,
        List<string> setClauses,
        List<AttributeValue> setParameters,
        List<string> removeClauses)
    {
        if (!complexCollectionEntry.IsModified)
            return;

        if (complexCollectionEntry.CurrentValue is null)
        {
            removeClauses.Add(path);
            return;
        }

        setClauses.Add($"{path} = ?");
        setParameters.Add(
            EntityWritePlan.SerializeComplexProperty(
                complexCollectionEntry.CurrentValue,
                complexProperty));
    }

    /// <summary>
    ///     Returns <see langword="true" /> when a complex reference should be replaced as a full
    ///     map instead of emitting nested-path scalar updates. This is needed when the original
    ///     reference was effectively missing (all tracked scalar originals are null/default), so a
    ///     nested path would target an absent DynamoDB parent attribute.
    /// </summary>
    private static bool ShouldReplaceWholeComplexProperty(
        ComplexPropertyEntry complexEntry,
        IComplexProperty complexProperty)
    {
        var hasModifiedMember = false;

        foreach (var property in complexProperty.ComplexType.GetProperties())
        {
            if (property.IsRuntimeOnly())
                continue;

            var propertyEntry = complexEntry.Property(property);
            if (!propertyEntry.IsModified)
                continue;

            hasModifiedMember = true;
            if (!IsDefaultOrNull(propertyEntry.OriginalValue, property.ClrType))
                return false;
        }

        foreach (var nestedComplexProperty in complexProperty.ComplexType.GetComplexProperties())
        {
            if (nestedComplexProperty.IsCollection)
            {
                var nestedCollectionEntry = complexEntry.ComplexCollection(nestedComplexProperty);
                if (!nestedCollectionEntry.IsModified)
                    continue;

                // A modified nested collection always requires replacing the containing map, but
                // sibling members may still prove the original map already existed.
                hasModifiedMember = true;
            }
            else
            {
                var nestedEntry = complexEntry.ComplexProperty(nestedComplexProperty);
                if (!nestedEntry.IsModified)
                    continue;

                hasModifiedMember = true;
                if (!ShouldReplaceWholeComplexProperty(nestedEntry, nestedComplexProperty))
                    return false;
            }
        }

        return hasModifiedMember;
    }

    private static bool IsDefaultOrNull(object? value, Type clrType)
    {
        if (value is null)
            return true;

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (!nonNullableType.IsValueType)
            return false;

        return value.Equals(Activator.CreateInstance(nonNullableType));
    }

    private static bool IsScalarModifiedProperty(IProperty property)
    {
        var converter = property.GetTypeMapping().Converter;
        var shapeType = converter?.ProviderClrType ?? property.ClrType;
        shapeType = Nullable.GetUnderlyingType(shapeType) ?? shapeType;

        if (shapeType == typeof(byte[]))
            return true;

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(shapeType, out _, out _)
            || DynamoTypeMappingSource.TryGetSetElementType(shapeType, out _)
            || DynamoTypeMappingSource.TryGetListElementType(shapeType, out _))
            return false;

        return true;
    }

    private static string EscapeIdentifier(string identifier)
        => identifier.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string EscapeStringLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private void AppendConcurrencyTokenPredicates(
        IUpdateEntry entry,
        IEntityType entityType,
        List<string> whereClauses,
        List<AttributeValue> whereParameters)
    {
        foreach (var property in entityType.GetProperties())
        {
            if (!property.IsConcurrencyToken || property.IsPrimaryKey())
                continue;

            whereParameters.Add(
                serializerSource.GetOrBuildOriginalValueSerializer(property)(entry));
            whereClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
        }
    }

    private void AppendConcurrencyTokenPredicates(
        IUpdateEntry entry,
        IEntityType entityType,
        StringBuilder sqlBuilder,
        List<AttributeValue> parameters)
    {
        List<string> whereClauses = [];
        List<AttributeValue> whereParameters = [];

        AppendConcurrencyTokenPredicates(entry, entityType, whereClauses, whereParameters);

        foreach (var clause in whereClauses)
            sqlBuilder.Append($" AND {clause}");

        parameters.AddRange(whereParameters);
    }
}
