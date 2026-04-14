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
    internal TransactionTargetItem BuildTargetItemIdentity(IUpdateEntry entry, string tableName)
    {
        var entityType = entry.EntityType;

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var partitionKeyValue = SerializeIdentityValue(entry, partitionKeyProperty);

        var sortKeyProperty = entityType.GetSortKeyProperty();
        var sortKeyValue = sortKeyProperty is null
            ? ""
            : SerializeIdentityValue(entry, sortKeyProperty);

        return new TransactionTargetItem(tableName, partitionKeyValue, sortKeyValue);
    }

    internal (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedUpdateStatement(
            IUpdateEntry entry,
            Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;
        var stateManager = rootEntry.StateManager;

        var setClauses = new List<string>();
        var setParameters = new List<AttributeValue>();

        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property))
                continue;

            if (property.IsPrimaryKey())
                throw new NotSupportedException(
                    $"SaveChanges Modified path does not support key mutation for "
                    + $"'{entityType.DisplayName()}.{property.Name}'.");

            if (!IsScalarModifiedProperty(property))
                continue;

            setClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            setParameters.Add(serializerSource.SerializeProperty(entry, property));
        }

        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property) || property.IsPrimaryKey())
                continue;

            if (IsScalarModifiedProperty(property))
                continue;

            setClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            setParameters.Add(serializerSource.SerializeProperty(entry, property));
        }

        return FinalizeUpdateStatement(
            entry,
            entityType,
            tableName,
            rootEntry,
            stateManager,
            setClauses,
            setParameters,
            mutatingNavs);
    }

    internal (string tableName, string sql, List<AttributeValue> parameters)?
        BuildOwnedMutationUpdateStatement(
            IUpdateEntry entry,
            Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;
        var stateManager = rootEntry.StateManager;

        return FinalizeUpdateStatement(
            entry,
            entityType,
            tableName,
            rootEntry,
            stateManager,
            [],
            [],
            mutatingNavs);
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

    private string SerializeIdentityValue(IUpdateEntry entry, IProperty keyProperty)
    {
        var value = serializerSource.GetOrBuildOriginalValueSerializer(keyProperty)(entry);
        return SerializeKeyAttributeValue(value, entry.EntityType, keyProperty);
    }

    private static string SerializeKeyAttributeValue(
        AttributeValue value,
        IEntityType entityType,
        IProperty keyProperty)
    {
        if (value.S is not null)
            return "S:" + value.S;
        if (value.N is not null)
            return "N:" + value.N;
        if (value.B is not null)
            return "B:" + Convert.ToBase64String(value.B.ToArray());

        throw new InvalidOperationException(
            $"Key property '{entityType.DisplayName()}.{keyProperty.Name}' produced "
            + "an unsupported DynamoDB key shape for transaction target identity "
            + "comparison. Only S, N, and B are supported.");
    }

    private (string tableName, string sql, List<AttributeValue> parameters)?
        FinalizeUpdateStatement(
            IUpdateEntry entry,
            IEntityType entityType,
            string tableName,
            InternalEntityEntry rootEntry,
            IStateManager stateManager,
            List<string> setClauses,
            List<AttributeValue> setParameters,
            Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var removeClauses = new List<string>();
        var whereParameters = new List<AttributeValue>();

        foreach (var nav in entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && !n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(nav, rootEntry, mutatingNavs))
                continue;

            var navAttrName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;
            var pathPrefix = $"\"{EscapeIdentifier(navAttrName)}\"";

            var navValue = entry.GetCurrentValue(nav);
            var ownedEntry = navValue is not null
                ? stateManager.TryGetEntry(navValue, nav.TargetEntityType)
                : null;

            if (ownedEntry is null || ownedEntry.EntityState == EntityState.Deleted)
            {
                removeClauses.Add(pathPrefix);
            }
            else if (ownedEntry.EntityState == EntityState.Added)
            {
                setClauses.Add($"{pathPrefix} = ?");
                setParameters.Add(
                    new AttributeValue
                    {
                        M = serializerSource.BuildItemFromOwnedEntry(ownedEntry),
                    });
            }
            else
            {
                AppendOwnedOneNestedSetClauses(
                    ownedEntry,
                    pathPrefix,
                    setClauses,
                    setParameters,
                    removeClauses,
                    mutatingNavs);
            }
        }

        foreach (var nav in entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(nav, rootEntry, mutatingNavs))
                continue;

            var navAttrName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;
            var path = $"\"{EscapeIdentifier(navAttrName)}\"";
            var navValue = entry.GetCurrentValue(nav);
            if (navValue is null)
            {
                removeClauses.Add(path);
                continue;
            }

            var elements = BuildOwnedManyElements(navValue, nav, stateManager);

            setClauses.Add($"{path} = ?");
            setParameters.Add(new AttributeValue { L = elements });
        }

        if (setClauses.Count == 0 && removeClauses.Count == 0)
            return null;

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

    private void AppendOwnedOneNestedSetClauses(
        InternalEntityEntry ownedEntry,
        string pathPrefix,
        List<string> setClauses,
        List<AttributeValue> setParameters,
        List<string> removeClauses,
        Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var stateManager = ownedEntry.StateManager;

        foreach (var property in ownedEntry.EntityType.GetProperties())
        {
            if (!ownedEntry.IsModified(property) || property.IsPrimaryKey())
                continue;

            var propPath = $"{pathPrefix}.\"{EscapeIdentifier(property.GetAttributeName())}\"";

            setClauses.Add($"{propPath} = ?");
            setParameters.Add(serializerSource.SerializeProperty(ownedEntry, property));
        }

        foreach (var subNav in ownedEntry
            .EntityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && !n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(subNav, ownedEntry, mutatingNavs))
                continue;

            var subNavAttrName =
                subNav.TargetEntityType.GetContainingAttributeName() ?? subNav.Name;
            var subPath = $"{pathPrefix}.\"{EscapeIdentifier(subNavAttrName)}\"";

            var subNavValue = ownedEntry.GetCurrentValue(subNav);
            var subEntry = subNavValue is not null
                ? stateManager.TryGetEntry(subNavValue, subNav.TargetEntityType)
                : null;

            if (subEntry is null || subEntry.EntityState == EntityState.Deleted)
            {
                removeClauses.Add(subPath);
            }
            else if (subEntry.EntityState == EntityState.Added)
            {
                setClauses.Add($"{subPath} = ?");
                setParameters.Add(
                    new AttributeValue { M = serializerSource.BuildItemFromOwnedEntry(subEntry) });
            }
            else
            {
                AppendOwnedOneNestedSetClauses(
                    subEntry,
                    subPath,
                    setClauses,
                    setParameters,
                    removeClauses,
                    mutatingNavs);
            }
        }

        foreach (var subNav in ownedEntry
            .EntityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(subNav, ownedEntry, mutatingNavs))
                continue;

            var subNavAttrName =
                subNav.TargetEntityType.GetContainingAttributeName() ?? subNav.Name;
            var subPath = $"{pathPrefix}.\"{EscapeIdentifier(subNavAttrName)}\"";
            var subNavValue = ownedEntry.GetCurrentValue(subNav);
            if (subNavValue is null)
            {
                removeClauses.Add(subPath);
                continue;
            }

            var elements = BuildOwnedManyElements(subNavValue, subNav, stateManager);

            setClauses.Add($"{subPath} = ?");
            setParameters.Add(new AttributeValue { L = elements });
        }
    }

    private List<AttributeValue> BuildOwnedManyElements(
        object navValue,
        INavigation nav,
        IStateManager stateManager)
    {
        var elements = new List<AttributeValue>();
        if (navValue is not IEnumerable collection)
            throw new InvalidOperationException(
                $"Owned collection navigation '{nav.DeclaringEntityType.DisplayName()}.{nav.Name}' "
                + "must be enumerable when non-null.");

        foreach (var element in collection)
        {
            if (element is null)
                continue;

            var ownedEntry = stateManager.TryGetEntry(element, nav.TargetEntityType);
            if (ownedEntry is null)
                throw new InvalidOperationException(
                    $"A collection element of type '{element.GetType().Name}' in navigation "
                    + $"'{nav.DeclaringEntityType.DisplayName()}.{nav.Name}' is not tracked by "
                    + "the change tracker. All owned collection elements must be tracked before "
                    + "calling SaveChanges. Use EF Core navigation fix-up or explicitly "
                    + "Add/Attach the element through the DbContext.");

            if (ownedEntry.EntityState == EntityState.Deleted)
                continue;

            elements.Add(
                new AttributeValue { M = serializerSource.BuildItemFromOwnedEntry(ownedEntry) });
        }

        return elements;
    }

    private static bool HasMutationForOwnedNavigation(
        INavigation nav,
        InternalEntityEntry principalEntry,
        Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
        => mutatingNavs.TryGetValue(principalEntry, out var set) && set.Contains(nav);

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
