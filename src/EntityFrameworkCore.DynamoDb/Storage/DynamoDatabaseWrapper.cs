using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry.SetProperty

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
    /// Persists <see cref="EntityState.Added"/> entries as PartiQL <c>INSERT</c> statements,
    /// scalar/simple <see cref="EntityState.Modified"/> entries as PartiQL <c>UPDATE</c>
    /// statements, and <see cref="EntityState.Deleted"/> entries as key-targeted PartiQL
    /// <c>DELETE</c> statements.
    /// </summary>
    /// <param name="entries">The tracked entity changes to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The number of root entities written.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when an entry has an unsupported state or when a Modified entry contains
    /// unsupported mutations.
    /// </exception>
    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        // Guard: only Added/Modified/Deleted are implemented; fail explicitly for others.
        // Must run before PromoteMutatingOwnedRoots to avoid mutating DbContext state on a
        // batch that will ultimately throw.
        var unsupported = entries.FirstOrDefault(static e
            => e.EntityState is not EntityState.Added
                and not EntityState.Modified
                and not EntityState.Deleted
            && !e.EntityType.IsOwned());

        if (unsupported is not null)
            throw new NotSupportedException(
                $"SaveChanges for EntityState.{unsupported.EntityState} is not yet supported. "
                + "Only Added, Modified, and Deleted entities can be persisted in this version.");

        PromoteMutatingOwnedRoots(entries);

        var rootEntries = entries
            .Where(static e
                => !e.EntityType.IsOwned()
                && (e.EntityState == EntityState.Added
                    || e.EntityState == EntityState.Modified
                    || e.EntityState == EntityState.Deleted))
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
                    // Concurrency: PartiQL INSERT fails with DuplicateItemException when a key
                    // already exists. This is the correct insert-only (create-never-replace)
                    // semantic; we map it to DbUpdateException below.

                    var item = serializerSource.BuildItem(entry);
                    var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
                    var (sql, parameters) = BuildInsertStatement(tableName, item);

                    commandLogger.ExecutingPartiQlQuery(tableName, sql);

                    try
                    {
                        await clientWrapper
                            .ExecuteWriteAsync(sql, parameters, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is DuplicateItemException
                            or TransactionCanceledException
                        || IsDuplicateKeyException(ex))
                    {
                        throw WrapWriteException(ex, EntityState.Added, entry);
                    }

                    break;
                }

                case EntityState.Modified:
                {
                    var update = BuildModifiedScalarUpdateStatement(entry, entries);
                    if (update is null)
                        continue;

                    commandLogger.ExecutingPartiQlWrite(update.Value.tableName, update.Value.sql);

                    try
                    {
                        await clientWrapper
                            .ExecuteWriteAsync(
                                update.Value.sql,
                                update.Value.parameters,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is ConditionalCheckFailedException
                        or TransactionCanceledException)
                    {
                        throw WrapWriteException(ex, EntityState.Modified, entry);
                    }

                    break;
                }

                case EntityState.Deleted:
                {
                    // Deleting the root item removes the whole DynamoDB document including all
                    // owned sub-entities — no separate statement is needed per owned entry.
                    // DynamoDB PartiQL DELETE on a missing key is a silent no-op even when
                    // concurrency-token WHERE predicates are present.
                    var delete = BuildDeleteStatement(entry);

                    commandLogger.ExecutingPartiQlWrite(delete.tableName, delete.sql);

                    try
                    {
                        await clientWrapper
                            .ExecuteWriteAsync(delete.sql, delete.parameters, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is ConditionalCheckFailedException
                        or TransactionCanceledException)
                    {
                        throw WrapWriteException(ex, EntityState.Deleted, entry);
                    }

                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"SaveChanges for EntityState.{entry.EntityState} is not handled "
                        + $"in the write loop for '{entry.EntityType.DisplayName()}'.");
            }

            rowsAffected++;
        }

        return rowsAffected;
    }

    /// <summary>
    ///     Builds a PartiQL <c>UPDATE</c> statement for a root entity in the
    ///     <see cref="EntityState.Modified" /> state, covering only scalar (non-collection) property
    ///     changes.
    /// </summary>
    /// <param name="entry">The root entity entry to update.</param>
    /// <param name="entries">
    ///     The full change set for this SaveChanges call, used to detect owned mutations
    ///     on the root.
    /// </param>
    /// <returns>
    ///     A tuple of (tableName, sql, parameters) if there are scalar changes to
    ///     persist, or <see langword="null" /> if the entity has no scalar property changes (e.g.
    ///     the root was promoted solely because an owned entry changed).
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when a modified property is a primary key (key
    ///     mutation is not supported), a collection type (M/L/SS/NS/BS attributes are not yet supported in
    ///     the update path), or when the root has no scalar changes but does have owned mutations (which
    ///     would require a full document rewrite — not yet implemented).
    /// </exception>
    private static (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedScalarUpdateStatement(IUpdateEntry entry, IEnumerable<IUpdateEntry> entries)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;

        var setClauses = new List<string>();

        // SET and WHERE parameters are kept separate so version management can add to both
        // independently before they are concatenated. The final parameter list order must match
        // the positional ? placeholders in the SQL text: SET values first, then WHERE values.
        var setParameters = new List<AttributeValue>();
        var whereParameters = new List<AttributeValue>();

        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property))
                continue;

            // DynamoDB items are identified by their key — mutating a key would require
            // deleting the old item and inserting a new one, which is not atomic and is
            // not supported.
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
            setParameters.Add(mapping.CreateAttributeValue(entry.GetCurrentValue(property)));
        }

        if (setClauses.Count == 0)
        {
            // The root has no scalar changes of its own. This happens when
            // PromoteModifiedOwnedRoots added this root entry because one of its owned entities
            // changed. Owned/nested mutations require a full document rewrite (read-modify-write),
            // which is not yet supported — fail explicitly rather than silently no-op'ing.
            if (HasOwnedMutationForRoot((InternalEntityEntry)entry, entries))
                throw new NotSupportedException(
                    $"SaveChanges Modified path for '{entityType.DisplayName()}' contains owned "
                    + "or nested mutations, which are not supported in this version.");

            return null;
        }

        // Key WHERE conditions: use original values so the correct DynamoDB item is targeted.
        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var partitionKeyMapping = partitionKeyProperty.GetTypeMapping() as DynamoTypeMapping
            ?? throw new InvalidOperationException(
                $"Partition key property '{entityType.DisplayName()}.{partitionKeyProperty.Name}' "
                + "requires a DynamoTypeMapping.");

        whereParameters.Add(
            partitionKeyMapping.CreateAttributeValue(
                GetOriginalOrCurrentValue(entry, partitionKeyProperty)));

        // Build WHERE conditions as a separate list so concurrency predicates can be appended.
        var whereClauses = new List<string>
        {
            $"\"{EscapeIdentifier(partitionKeyProperty.GetAttributeName())}\" = ?",
        };

        var sortKeyProperty = entityType.GetSortKeyProperty();
        if (sortKeyProperty is not null)
        {
            var sortKeyMapping = sortKeyProperty.GetTypeMapping() as DynamoTypeMapping
                ?? throw new InvalidOperationException(
                    $"Sort key property '{entityType.DisplayName()}.{sortKeyProperty.Name}' "
                    + "requires a DynamoTypeMapping.");

            whereParameters.Add(
                sortKeyMapping.CreateAttributeValue(
                    GetOriginalOrCurrentValue(entry, sortKeyProperty)));

            whereClauses.Add($"\"{EscapeIdentifier(sortKeyProperty.GetAttributeName())}\" = ?");
        }

        AppendConcurrencyTokenPredicates(entry, entityType, whereClauses, whereParameters);

        var sql = new StringBuilder()
            .AppendLine($"UPDATE \"{EscapeIdentifier(tableName)}\"")
            .AppendLine($"SET {string.Join(", ", setClauses)}")
            .Append($"WHERE {string.Join(" AND ", whereClauses)}")
            .ToString();

        var parameters = new List<AttributeValue>(setParameters.Count + whereParameters.Count);
        parameters.AddRange(setParameters);
        parameters.AddRange(whereParameters);

        return (tableName, sql, parameters);
    }

    /// <summary>
    ///     Returns the original value of <paramref name="property" /> when EF Core is tracking it,
    ///     falling back to the current value otherwise.
    /// </summary>
    /// <remarks>
    ///     Original values are used for key properties in WHERE clauses so that the correct DynamoDB
    ///     item is targeted even if the in-memory value has been touched (key mutation is rejected
    ///     separately before this is called).
    /// </remarks>
    private static object? GetOriginalOrCurrentValue(IUpdateEntry entry, IProperty property)
        => entry.CanHaveOriginalValue(property)
            ? entry.GetOriginalValue(property)
            : entry.GetCurrentValue(property);

    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="property" /> can be written via the
    ///     scalar <c>SET</c> path — i.e. it is not a DynamoDB collection type (Map, List, Set).
    /// </summary>
    /// <remarks>
    ///     Collection attributes (M, L, SS, NS, BS) require a full document rewrite or DynamoDB
    ///     update expressions beyond simple scalar assignment and are not supported in this path.
    ///     <c>byte[]</c> is a binary scalar (B attribute) and is explicitly allowed.
    /// </remarks>
    private static bool SupportsScalarModifiedProperty(IProperty property)
    {
        var converter = property.GetTypeMapping().Converter;
        var shapeType = converter?.ProviderClrType ?? property.ClrType;
        shapeType = Nullable.GetUnderlyingType(shapeType) ?? shapeType;

        // byte[] maps to a binary scalar (B attribute) — allow it before the collection checks.
        if (shapeType == typeof(byte[]))
            return true;

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(shapeType, out _, out _)
            || DynamoTypeMappingSource.TryGetSetElementType(shapeType, out _)
            || DynamoTypeMappingSource.TryGetListElementType(shapeType, out _))
            return false;

        return true;
    }

    /// <summary>
    ///     Doubles any double-quote characters in <paramref name="identifier" /> to produce a safe
    ///     PartiQL quoted identifier.
    /// </summary>
    private static string EscapeIdentifier(string identifier)
        => identifier.Replace("\"", "\"\"", StringComparison.Ordinal);

    /// <summary>
    ///     Ensures that the aggregate root of every mutating owned entity is also present in
    ///     <paramref name="entries" /> and is in at least the <see cref="EntityState.Modified" /> state,
    ///     so that <see cref="BuildModifiedScalarUpdateStatement" /> is called for it and can detect
    ///     whether the mutation is supported.
    /// </summary>
    /// <remarks>
    ///     EF Core tracks owned entity changes independently from their root. When only an owned
    ///     sub-entity mutates (Added/Modified/Deleted), the root entry may still be
    ///     <see cref="EntityState.Unchanged" /> and absent from the entries list. We promote it here
    ///     (with <c>modifyProperties: false</c> to avoid marking all scalar properties as dirty) so that
    ///     the write loop sees it and can either emit an update (if the owned-mutation path is later
    ///     implemented) or fail with a clear error.
    /// </remarks>
    private static void PromoteMutatingOwnedRoots(IList<IUpdateEntry> entries)
    {
        var count = entries.Count;
        var trackedEntries =
            new HashSet<InternalEntityEntry>(count, ReferenceEqualityComparer.Instance);

        for (var i = 0; i < count; i++)
            trackedEntries.Add((InternalEntityEntry)entries[i]);

        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            if (!entry.EntityType.IsOwned()
                || entry.EntityState is not EntityState.Modified
                    and not EntityState.Added
                    and not EntityState.Deleted)
                continue;

            var root = GetRootEntry((InternalEntityEntry)entry);

            if (root.EntityState == EntityState.Unchanged)
                root.SetEntityState(EntityState.Modified, modifyProperties: false);

            if (trackedEntries.Add(root))
                entries.Add(root);
        }
    }

    /// <summary>
    ///     Returns <see langword="true" /> if any owned entity in <paramref name="entries" /> with an
    ///     active mutation (Added, Modified, or Deleted) belongs to <paramref name="root" />.
    /// </summary>
    /// <remarks>
    ///     Used to distinguish a root that has no scalar changes because nothing actually changed
    ///     from one that has no scalar changes because only its owned sub-graph changed. The latter
    ///     requires a different (not yet implemented) write strategy.
    /// </remarks>
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

    /// <summary>
    ///     Walks the EF Core ownership chain to find the non-owned aggregate root of
    ///     <paramref name="entry" />.
    /// </summary>
    private static InternalEntityEntry GetRootEntry(InternalEntityEntry entry)
    {
        while (true)
        {
            if (!entry.EntityType.IsOwned())
                return entry;

            var ownership =
                entry.EntityType.FindOwnership()
                ?? throw new InvalidOperationException(
                    $"Owned entity type '{entry.EntityType.DisplayName()}' has no ownership metadata.");

            var principal =
                entry.StateManager.FindPrincipal(entry, ownership)
                ?? throw new InvalidOperationException(
                    $"Owned entity '{entry.EntityType.DisplayName()}' is orphaned from its principal "
                    + $"'{ownership.PrincipalEntityType.DisplayName()}'.");

            // Recurse in case of multi-level ownership (e.g. Root → Owned → NestedOwned).
            if (principal.EntityType.IsOwned())
            {
                entry = principal;
                continue;
            }

            return principal;
        }
    }

    /// <summary>
    /// Generates a key-targeted PartiQL <c>DELETE FROM "table" WHERE "pk" = ? [AND "sk" = ?] [AND "token" = ?]</c>
    /// statement for a root entity in the <see cref="EntityState.Deleted"/> state.
    /// Configured concurrency tokens are included in the WHERE predicate with their original
    /// values. If the store values have changed since the entity was read,
    /// <c>ConditionalCheckFailedException</c> fires.
    /// </summary>
    /// <param name="entry">The root entity entry to delete.</param>
    /// <returns>A tuple of (tableName, sql, parameters) for the DELETE statement.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity type does not define a partition key or when a key property
    /// lacks a <see cref="DynamoTypeMapping"/>.
    /// </exception>
    private static (string tableName, string sql, List<AttributeValue> parameters)
        BuildDeleteStatement(IUpdateEntry entry)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;

        // Guard: same safety net as BuildInsertStatement — DynamoDB table names cannot contain
        // double-quotes, but a bad annotation value would otherwise silently corrupt the
        // identifier.
        if (tableName.Contains('"'))
            throw new ArgumentException(
                $"Table name '{tableName}' contains an illegal character ('\"'). "
                + "DynamoDB table names must not contain double-quote characters.",
                nameof(tableName));

        var entityType = entry.EntityType;

        var partitionKeyProperty = entityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");

        var partitionKeyMapping = partitionKeyProperty.GetTypeMapping() as DynamoTypeMapping
            ?? throw new InvalidOperationException(
                $"Partition key property '{entityType.DisplayName()}.{partitionKeyProperty.Name}' "
                + "requires a DynamoTypeMapping.");

        // Use the original key value so that the correct DynamoDB item is targeted even if the
        // in-memory value was touched before deletion was requested.
        var parameters = new List<AttributeValue>
        {
            partitionKeyMapping.CreateAttributeValue(
                GetOriginalOrCurrentValue(entry, partitionKeyProperty)),
        };

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine($"DELETE FROM \"{EscapeIdentifier(tableName)}\"");
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

            sqlBuilder.Append(
                $" AND \"{EscapeIdentifier(sortKeyProperty.GetAttributeName())}\" = ?");
        }

        AppendConcurrencyTokenPredicates(entry, entityType, sqlBuilder, parameters);

        return (tableName, sqlBuilder.ToString(), parameters);
    }

    /// <summary>
    /// Generates a PartiQL <c>INSERT INTO "table" VALUE {'key': ?, ...}</c> statement with
    /// one positional parameter per top-level attribute in <paramref name="item"/>.
    /// </summary>
    /// <remarks>
    /// Attribute keys are emitted as single-quoted string literals inside the <c>VALUE</c> document
    /// clause. Embedded single quotes in keys are escaped by doubling (<c>'</c> → <c>''</c>).
    /// Table names are validated separately.
    /// </remarks>
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

            // Keys are string literals in the VALUE document clause. Escape embedded single quotes.
            sql.Append($"'{EscapeStringLiteral(key)}': ?");
            parameters.Add(value);
            first = false;
        }

        sql.Append('}');
        return (sql.ToString(), parameters);
    }

    private static string EscapeStringLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    ///     Returns <see langword="true" /> when <paramref name="ex" /> represents a duplicate primary
    ///     key error regardless of whether it was surfaced as the typed
    ///     <see cref="DuplicateItemException" /> or as the generic <see cref="AmazonDynamoDBException" />
    ///     (which DynamoDB Local emits).
    /// </summary>
    private static bool IsDuplicateKeyException(Exception ex)
        => ex is AmazonDynamoDBException ade
            && string.Equals(ade.ErrorCode, "DuplicateItem", StringComparison.Ordinal);

    /// <summary>
    ///     Appends WHERE predicates and parameter values for all configured non-key concurrency token
    ///     properties on <paramref name="entityType" />, using tracked original values.
    /// </summary>
    private static void AppendConcurrencyTokenPredicates(
        IUpdateEntry entry,
        IEntityType entityType,
        List<string> whereClauses,
        List<AttributeValue> whereParameters)
    {
        foreach (var property in entityType.GetProperties())
        {
            if (!property.IsConcurrencyToken || property.IsPrimaryKey())
                continue;

            var mapping = property.GetTypeMapping() as DynamoTypeMapping
                ?? throw new InvalidOperationException(
                    $"Property '{entityType.DisplayName()}.{property.Name}' "
                    + "requires a DynamoTypeMapping.");

            whereParameters.Add(
                mapping.CreateAttributeValue(GetOriginalOrCurrentValue(entry, property)));
            whereClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
        }
    }

    /// <summary>
    ///     Appends SQL and parameter values for all configured non-key concurrency tokens to an
    ///     existing DELETE statement builder.
    /// </summary>
    private static void AppendConcurrencyTokenPredicates(
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

    /// <summary>
    ///     Maps a raw DynamoDB write exception to the appropriate EF Core exception type.
    ///     <list type="bullet">
    ///         <item>
    ///             <see cref="DuplicateItemException" /> →
    ///             <see cref="DbUpdateException" /> (INSERT key conflict)
    ///         </item>
    ///         <item>
    ///             <see cref="ConditionalCheckFailedException" /> →
    ///             <see cref="DbUpdateConcurrencyException" /> (stale concurrency token)
    ///         </item>
    ///         <item>
    ///             <see cref="TransactionCanceledException" /> with <c>ConditionalCheckFailed</c> reason →
    ///             <see cref="DbUpdateConcurrencyException" />
    ///         </item>
    ///         <item>All other exceptions → <see cref="DbUpdateException" /></item>
    ///     </list>
    /// </summary>
    private static DbUpdateException WrapWriteException(
        Exception ex,
        EntityState entityState,
        IUpdateEntry entry)
    {
        if (ex is DuplicateItemException || IsDuplicateKeyException(ex))
            return new DbUpdateException(
                $"Cannot insert '{entry.EntityType.DisplayName()}': an item with the same primary "
                + "key already exists.",
                ex,
                [entry]);

        if (ex is ConditionalCheckFailedException)
            return new DbUpdateConcurrencyException(
                $"The '{entry.EntityType.DisplayName()}' entity could not be "
                + (entityState == EntityState.Modified ? "updated" : "deleted")
                + " because one or more concurrency token values have changed since it was last read. "
                + "Another writer may have modified this item.",
                ex,
                [entry]);

        if (ex is TransactionCanceledException tce)
        {
            // A transaction may be cancelled for multiple reasons; only map to concurrency if at
            // least one reason is a ConditionalCheckFailed (i.e. a version predicate mismatch).
            var hasConcurrency = tce.CancellationReasons?.Any(static r
                    => string.Equals(r.Code, "ConditionalCheckFailed", StringComparison.Ordinal))
                ?? false;

            return hasConcurrency
                ? new DbUpdateConcurrencyException(
                    $"Transaction cancelled due to a concurrency token conflict on "
                    + $"'{entry.EntityType.DisplayName()}'.",
                    tce,
                    [entry])
                : new DbUpdateException(
                    $"Transaction cancelled while saving '{entry.EntityType.DisplayName()}'.",
                    tce,
                    [entry]);
        }

        return new DbUpdateException(
            $"An error occurred saving '{entry.EntityType.DisplayName()}' to DynamoDB.",
            ex,
            [entry]);
    }
}
