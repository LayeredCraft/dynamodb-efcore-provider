using System.Collections;
using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoPartiqlStatementFactory(
    DynamoEntityItemSerializerSource serializerSource)
{
    internal (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedUpdateStatement(IUpdateEntry entry)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;

        var setClauses = new List<string>();
        var setParameters = new List<AttributeValue>();
        var scalarAssignments = new List<(string Clause, AttributeValue Parameter)>();
        var nonScalarAssignments = new List<(string Clause, AttributeValue Parameter)>();

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
        // The entire M / L attribute is written atomically — no partial nested update.
        var entity = rootEntry.Entity;
        foreach (var cp in entityType.GetComplexProperties())
        {
            if (!IsComplexPropertyModified(entry, cp))
                continue;

            var cpKey = ((IReadOnlyComplexProperty)cp).GetAttributeName();
            var cpValue = cp.GetGetter().GetClrValue(entity);
            setClauses.Add($"\"{EscapeIdentifier(cpKey)}\" = ?");
            setParameters.Add(EntityWritePlan.SerializeComplexProperty(cpValue, cp));
        }

        return FinalizeUpdateStatement(entry, entityType, tableName, setClauses, setParameters);
    }

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
            List<AttributeValue> setParameters)
    {
        if (setClauses.Count == 0)
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

        sqlBuilder.AppendLine($"SET {string.Join(", ", setClauses)}");
        sqlBuilder.Append($"WHERE {string.Join(" AND ", whereClauses)}");

        var parameters = new List<AttributeValue>(setParameters.Count + whereParameters.Count);
        parameters.AddRange(setParameters);
        parameters.AddRange(whereParameters);

        return (tableName, sqlBuilder.ToString(), parameters);
    }

    /// <summary>
    ///     Returns <see langword="true" /> when a complex property (or any of its nested scalar
    ///     properties) is modified in the current entry snapshot.
    /// </summary>
    private static bool IsComplexPropertyModified(IUpdateEntry entry, IComplexProperty cp)
        => IsComplexTypeModified(entry, cp.ComplexType);

    private static bool IsComplexTypeModified(IUpdateEntry entry, IComplexType complexType)
    {
        foreach (var property in complexType.GetProperties())
            if (!property.IsRuntimeOnly() && entry.IsModified(property))
                return true;

        foreach (var nestedCp in complexType.GetComplexProperties())
            if (IsComplexTypeModified(entry, nestedCp.ComplexType))
                return true;

        return false;
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
