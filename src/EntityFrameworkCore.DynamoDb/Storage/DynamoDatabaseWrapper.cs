using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Translates EF Core <see cref="SaveChanges"/> calls into PartiQL write statements and executes
/// them against DynamoDB via <see cref="IDynamoClientWrapper"/>.
/// </summary>
public class DynamoDatabaseWrapper(
    DatabaseDependencies dependencies,
    IDynamoClientWrapper clientWrapper,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    DynamoEntityItemSerializerSource serializerSource) : Database(dependencies)
{
    /// <summary>Not supported — DynamoDB only exposes an async API.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override int SaveChanges(IList<IUpdateEntry> entries)
        => throw new NotSupportedException(
            "DynamoDB does not support synchronous SaveChanges. Use SaveChangesAsync instead.");

    /// <summary>
    /// Persists <see cref="EntityState.Added"/> entries as PartiQL <c>INSERT</c> statements and
    /// scalar/simple <see cref="EntityState.Modified"/> entries as PartiQL <c>UPDATE</c>
    /// statements.
    /// </summary>
    /// <param name="entries">The tracked entity changes to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The number of root entities written.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when an entry has an unsupported state (for example
    /// <see cref="EntityState.Deleted"/>) or when a Modified entry contains unsupported mutations.
    /// </exception>
    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        PromoteModifiedOwnedRoots(entries);

        // Guard: only Added/Modified are implemented; fail explicitly for Deleted and others.
        var unsupported = entries.FirstOrDefault(static e
            => e.EntityState is not EntityState.Added and not EntityState.Modified
            && !e.EntityType.IsOwned());

        if (unsupported is not null)
            throw new NotSupportedException(
                $"SaveChanges for EntityState.{unsupported.EntityState} is not yet supported. "
                + "Only Added and Modified entities can be persisted in this version.");

        var rootEntries = entries
            .Where(static e
                => !e.EntityType.IsOwned()
                && (e.EntityState == EntityState.Added || e.EntityState == EntityState.Modified))
            .ToList();

        var rowsAffected = 0;

        foreach (var entry in rootEntries)
        {
            switch (entry.EntityState)
            {
                case EntityState.Added:
                {
                    // Serialization is handled by the compiled, per-entity-type serializer — no
                    // per-call type dispatch or value-type boxing on the scalar-property hot path.
                    // Owned sub-entries are resolved on-demand via the EF state manager.
                    //
                    // Concurrency note: for Added entities there is no prior version to conflict
                    // with.
                    // DynamoDB's PartiQL INSERT will fail with ConditionalCheckFailedException if
                    // the
                    // key already exists, which is the correct behavior for inserts. Optimistic
                    // concurrency token validation (version checks) will be required when Modified
                    // and
                    // Deleted entity states are implemented.
                    var item = serializerSource.BuildItem(entry);
                    var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
                    var (sql, parameters) = BuildInsertStatement(tableName, item);

                    commandLogger.ExecutingPartiQlQuery(tableName, sql);

                    await clientWrapper
                        .ExecuteWriteAsync(sql, parameters, cancellationToken)
                        .ConfigureAwait(false);

                    rowsAffected++;
                    break;
                }

                case EntityState.Modified:
                {
                    var update = BuildModifiedScalarUpdateStatement(entry, entries);
                    if (update is null)
                        continue;

                    commandLogger.ExecutingPartiQlWrite(update.Value.tableName, update.Value.sql);

                    await clientWrapper
                        .ExecuteWriteAsync(
                            update.Value.sql,
                            update.Value.parameters,
                            cancellationToken)
                        .ConfigureAwait(false);

                    rowsAffected++;
                    break;
                }
            }
        }

        return rowsAffected;
    }

    private static (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedScalarUpdateStatement(IUpdateEntry entry, IEnumerable<IUpdateEntry> entries)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;

        var setClauses = new List<string>();
        var parameters = new List<AttributeValue>();

        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property))
                continue;

            if (property.IsPrimaryKey())
                throw new NotSupportedException(
                    $"SaveChanges Modified path does not support key mutation for "
                    + $"'{entityType.DisplayName()}.{property.Name}'.");

            if (!SupportsScalarModifiedProperty(property))
                throw new NotSupportedException(
                    $"SaveChanges Modified path only supports scalar/simple properties in this "
                    + $"version. Property '{entityType.DisplayName()}.{property.Name}' is not "
                    + "supported yet.");

            var mapping = property.GetTypeMapping() as DynamoTypeMapping;
            if (mapping is null || !mapping.CanSerialize)
                throw new NotSupportedException(
                    $"Property '{entityType.DisplayName()}.{property.Name}' does not have a "
                    + "supported DynamoDB write mapping for Modified entities.");

            setClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            parameters.Add(mapping.CreateAttributeValue(entry.GetCurrentValue(property)));
        }

        if (setClauses.Count == 0)
        {
            if (HasOwnedMutationForRoot((InternalEntityEntry)entry, entries))
                throw new NotSupportedException(
                    $"SaveChanges Modified path for '{entityType.DisplayName()}' contains owned "
                    + "or nested mutations, which are not supported in this version.");

            return null;
        }

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var partitionKeyMapping = partitionKeyProperty.GetTypeMapping() as DynamoTypeMapping
            ?? throw new InvalidOperationException(
                $"Partition key property '{entityType.DisplayName()}.{partitionKeyProperty.Name}' "
                + "requires a DynamoTypeMapping.");

        parameters.Add(
            partitionKeyMapping.CreateAttributeValue(
                GetOriginalOrCurrentValue(entry, partitionKeyProperty)));

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine($"UPDATE \"{EscapeIdentifier(tableName)}\"");
        sqlBuilder.AppendLine($"SET {string.Join(", ", setClauses)}");
        sqlBuilder.Append(
            $"WHERE \"{EscapeIdentifier(partitionKeyProperty.GetAttributeName())}\" = ?");

        var sortKeyProperty = entityType.GetSortKeyProperty();
        if (sortKeyProperty is not null)
        {
            var sortKeyMapping = sortKeyProperty.GetTypeMapping() as DynamoTypeMapping
                ?? throw new InvalidOperationException(
                    $"Sort key property '{entityType.DisplayName()}.{sortKeyProperty.Name}' "
                    + "requires a DynamoTypeMapping.");

            parameters.Add(
                sortKeyMapping.CreateAttributeValue(
                    GetOriginalOrCurrentValue(entry, sortKeyProperty)));

            sqlBuilder.AppendLine();
            sqlBuilder.Append(
                $"AND \"{EscapeIdentifier(sortKeyProperty.GetAttributeName())}\" = ?");
        }

        var sql = sqlBuilder.ToString();

        return (tableName, sql, parameters);
    }

    private static object? GetOriginalOrCurrentValue(IUpdateEntry entry, IProperty property)
        => entry.CanHaveOriginalValue(property)
            ? entry.GetOriginalValue(property)
            : entry.GetCurrentValue(property);

    private static bool SupportsScalarModifiedProperty(IProperty property)
    {
        var clrType = property.ClrType;
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (nonNullableType == typeof(byte[]))
            return true;

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(clrType, out _, out _)
            || DynamoTypeMappingSource.TryGetSetElementType(clrType, out _)
            || DynamoTypeMappingSource.TryGetListElementType(clrType, out _))
            return false;

        return true;
    }

    private static string EscapeIdentifier(string identifier)
        => identifier.Replace("\"", "\"\"", StringComparison.Ordinal);

#pragma warning disable EF1001 // Internal EF Core API usage
    private static void PromoteModifiedOwnedRoots(IList<IUpdateEntry> entries)
    {
        var count = entries.Count;

        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            if (!entry.EntityType.IsOwned() || entry.EntityState != EntityState.Modified)
                continue;

            var root = GetRootEntry((InternalEntityEntry)entry);

            if (root.EntityState == EntityState.Unchanged)
                root.SetEntityState(EntityState.Modified, modifyProperties: false);

            if (!entries.Contains(root))
                entries.Add(root);
        }
    }

    private static bool HasOwnedMutationForRoot(
        InternalEntityEntry root,
        IEnumerable<IUpdateEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (!entry.EntityType.IsOwned()
                || entry.EntityState is not EntityState.Modified
                    and not EntityState.Added
                    and not EntityState.Deleted)
                continue;

            var candidateRoot = GetRootEntry((InternalEntityEntry)entry);
            if (ReferenceEquals(candidateRoot, root))
                return true;
        }

        return false;
    }

    private static InternalEntityEntry GetRootEntry(InternalEntityEntry entry)
    {
        if (!entry.EntityType.IsOwned())
            return entry;

        var ownership = entry.EntityType.FindOwnership()
            ?? throw new InvalidOperationException(
                $"Owned entity type '{entry.EntityType.DisplayName()}' has no ownership metadata.");

        var principal = entry.StateManager.FindPrincipal(entry, ownership)
            ?? throw new InvalidOperationException(
                $"Owned entity '{entry.EntityType.DisplayName()}' is orphaned from its principal "
                + $"'{ownership.PrincipalEntityType.DisplayName()}'.");

        return principal.EntityType.IsOwned() ? GetRootEntry(principal) : principal;
    }
#pragma warning restore EF1001 // Internal EF Core API usage

    /// <summary>
    /// Generates a PartiQL <c>INSERT INTO "table" VALUE {'key': ?, ...}</c> statement with
    /// one positional parameter per top-level attribute in <paramref name="item"/>.
    /// </summary>
    private static (string sql, List<AttributeValue> parameters) BuildInsertStatement(
        string tableName,
        Dictionary<string, AttributeValue> item)
    {
        // Guard: a double-quote in the table name would break the PartiQL identifier syntax.
        // DynamoDB table names only allow letters, digits, hyphens, dots, and underscores, so
        // this should never fire in practice but is a cheap safety net.
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

            // Keys are string literals in the VALUE document clause — reserved words are safe here.
            sql.Append($"'{key}': ?");
            parameters.Add(value);
            first = false;
        }

        sql.Append('}');
        return (sql.ToString(), parameters);
    }
}
