using System.Collections;
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
        var unsupported = entries.FirstOrDefault(static e
            => e.EntityState is not EntityState.Added
                and not EntityState.Modified
                and not EntityState.Deleted
            && !e.EntityType.IsOwned());

        if (unsupported is not null)
            throw new NotSupportedException(
                $"SaveChanges for EntityState.{unsupported.EntityState} is not yet supported. "
                + "Only Added, Modified, and Deleted entities can be persisted in this version.");

        var rootEntries = BuildRootEntries(entries);

        // Pre-compute once per save call: for each principal entry, which of its owned
        // navigations have at least one Add/Modify/Delete mutation in this batch?
        // Turns HasMutationForOwnedNavigation from O(entries × navs × depth) → O(1) per check.
        var mutatingNavs = BuildMutatingNavLookup(entries);

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

                    commandLogger.ExecutingPartiQlWrite(tableName, sql);

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
                    // Scalar/collection root properties AND/OR owned sub-entities may have
                    // changed — run all four phases (A: scalars, B: collections, C: OwnsOne,
                    // D: OwnsMany).
                    var update = BuildModifiedUpdateStatement(entry, mutatingNavs);
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

                case EntityState.Unchanged:
                {
                    // Root injected by IncludeMutatingOwnedRoots: its own scalar and collection
                    // properties have not changed (IsModified is false for all of them).
                    // Only owned sub-entities mutated — skip directly to phases C+D.
                    //
                    // We intentionally do not promote the root to Modified here (unlike the Cosmos
                    // provider) because our partial-UPDATE strategy depends on IsModified(property)
                    // being false for untouched root scalars. Promoting the state would mark every
                    // scalar as modified and generate spurious SET clauses.
                    var update = BuildOwnedMutationUpdateStatement(entry, mutatingNavs);
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
    ///     <see cref="EntityState.Modified" /> or <see cref="EntityState.Unchanged" /> state,
    ///     covering scalar properties, primitive collection properties (L/M/SS/NS/BS), owned
    ///     reference mutations, and owned collection mutations.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For modified scalar properties, a standard <c>SET "Prop" = ?</c> clause is emitted.
    ///     </para>
    ///     <para>
    ///         For primitive collection properties (List, Dictionary, Set), the entire attribute
    ///         value is replaced: <c>SET "Tags" = ?</c> with the fully serialized SS/NS/BS/L/M
    ///         value. EF Core tracks these as atomic property values with no element-level delta,
    ///         so full replacement is the correct strategy.
    ///     </para>
    ///     <para>
    ///         For OwnsOne navigations: Modified refs emit per-property nested-path SET clauses
    ///         (<c>SET "Profile"."DisplayName" = ?</c>); Added refs emit a full map replacement
    ///         (<c>SET "Profile" = ?</c>); Deleted refs emit <c>REMOVE "Profile"</c>. All cases
    ///         recurse into nested OwnsOne chains.
    ///     </para>
    ///     <para>
    ///         For OwnsMany navigations, the entire list attribute is replaced with all
    ///         non-deleted elements serialized in collection order.
    ///     </para>
    /// </remarks>
    /// <param name="entry">The root entity entry to update.</param>
    /// <param name="mutatingNavs">
    ///     Pre-built lookup from principal entry → set of owned navigations that have at
    ///     least one Add/Modify/Delete mutation in this batch. Used for O(1) mutation checks.
    /// </param>
    /// <returns>
    ///     A tuple of (tableName, sql, parameters) when there is something to write,
    ///     or <see langword="null" /> when the entity has no effective changes.
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when a modified property is a primary key (key mutation is not supported).
    /// </exception>
    private (string tableName, string sql, List<AttributeValue> parameters)?
        BuildModifiedUpdateStatement(
            IUpdateEntry entry,
            Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;
        var stateManager = rootEntry.StateManager;

        var setClauses = new List<string>();

        // SET and WHERE parameters are kept separate so version management can add to both
        // independently before they are concatenated. The final parameter list order must match
        // the positional ? placeholders in the SQL text: SET values first, then WHERE values.
        var setParameters = new List<AttributeValue>();
        var removeClauses = new List<string>();
        var whereParameters = new List<AttributeValue>();

        // Phase A — Scalar root properties (existing behavior)
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

            if (!IsScalarModifiedProperty(property))
                continue; // Collection properties are handled in Phase B

            var mapping = property.GetTypeMapping() as DynamoTypeMapping;
            if (mapping is null || !mapping.CanWriteToAttributeValue)
                throw new NotSupportedException(
                    $"Property '{entityType.DisplayName()}.{property.Name}' does not have a "
                    + "supported DynamoDB write mapping for Modified entities.");

            setClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            setParameters.Add(mapping.CreateAttributeValue(entry.GetCurrentValue(property)));
        }

        // Phase B — Primitive collection properties (L/M/SS/NS/BS)
        // Full attribute replacement: EF Core tracks the entire collection as a single property
        // value with no element-level delta, so SET "attr" = <full serialized value> is correct.
        foreach (var property in entityType.GetProperties())
        {
            if (!entry.IsModified(property) || property.IsPrimaryKey())
                continue;

            if (IsScalarModifiedProperty(property))
                continue; // Already handled in Phase A

            setClauses.Add($"\"{EscapeIdentifier(property.GetAttributeName())}\" = ?");
            setParameters.Add(serializerSource.SerializeProperty(entry, property));
        }

        // Phase C — OwnsOne navigations
        // Modified: nested-path SET per changed property (recurse for deep chains).
        // Added (null→ref): full M replace — nested SET paths require the parent attribute to
        // exist.
        // Deleted (ref→null): REMOVE the attribute entirely.
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
                // Modified — emit per-property nested-path SET (and REMOVE for sub-nulled refs)
                AppendOwnedOneNestedSetClauses(
                    ownedEntry,
                    pathPrefix,
                    setClauses,
                    setParameters,
                    removeClauses,
                    mutatingNavs);
            }
        }

        // Phase D — OwnsMany navigations
        // Full list replacement: serialize all non-Deleted elements in collection order.
        // list_append / REMOVE [i] are not viable because EF Core has no element-level index delta.
        foreach (var nav in entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(nav, rootEntry, mutatingNavs))
                continue;

            var navAttrName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;
            var elements = BuildOwnedManyElements(entry.GetCurrentValue(nav), nav, stateManager);

            setClauses.Add($"\"{EscapeIdentifier(navAttrName)}\" = ?");
            setParameters.Add(new AttributeValue { L = elements });
        }

        // If there is nothing to write (no scalar, collection, owned, or structural change),
        // return null to signal the caller to skip this entry.
        if (setClauses.Count == 0 && removeClauses.Count == 0)
            return null;

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
    ///     Recursively appends SET and REMOVE clauses for a Modified OwnsOne entry, emitting
    ///     fine-grained nested-path expressions such as <c>"Profile"."DisplayName" = ?</c>.
    /// </summary>
    /// <remarks>
    ///     Collection-typed properties within the owned entry are replaced atomically (
    ///     <c>SET "Profile"."Tags" = ?</c>). Nested OwnsOne sub-navigations follow the same
    ///     Added/Modified/Deleted branching as the top-level Phase C logic. Nested OwnsMany
    ///     sub-navigations use full list replacement.
    /// </remarks>
    private void AppendOwnedOneNestedSetClauses(
        InternalEntityEntry ownedEntry,
        string pathPrefix,
        List<string> setClauses,
        List<AttributeValue> setParameters,
        List<string> removeClauses,
        Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var stateManager = ownedEntry.StateManager;

        // Scalar and collection properties on the owned entry
        foreach (var property in ownedEntry.EntityType.GetProperties())
        {
            if (!ownedEntry.IsModified(property) || property.IsPrimaryKey())
                continue;

            var propPath = $"{pathPrefix}.\"{EscapeIdentifier(property.GetAttributeName())}\"";

            if (IsScalarModifiedProperty(property))
            {
                var mapping = property.GetTypeMapping() as DynamoTypeMapping;
                if (mapping is null || !mapping.CanWriteToAttributeValue)
                    continue;
                setClauses.Add($"{propPath} = ?");
                setParameters.Add(
                    mapping.CreateAttributeValue(ownedEntry.GetCurrentValue(property)));
            }
            else
            {
                // Collection property inside OwnsOne: full attribute replacement
                setClauses.Add($"{propPath} = ?");
                setParameters.Add(serializerSource.SerializeProperty(ownedEntry, property));
            }
        }

        // Nested OwnsOne sub-navigations: recurse
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

        // Nested OwnsMany sub-navigations: full list replacement
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
            var elements = BuildOwnedManyElements(
                ownedEntry.GetCurrentValue(subNav),
                subNav,
                stateManager);

            setClauses.Add($"{subPath} = ?");
            setParameters.Add(new AttributeValue { L = elements });
        }
    }

    /// <summary>
    ///     Builds the list of serialized <see cref="AttributeValue" /> elements for an OwnsMany
    ///     navigation by iterating the current CLR collection and serializing all non-Deleted entries.
    ///     Deleted elements are absent from the CLR collection when EF Core removes them, but any
    ///     remaining tracked entries whose state is <see cref="EntityState.Deleted" /> are also skipped as
    ///     a safety guard.
    /// </summary>
    private List<AttributeValue> BuildOwnedManyElements(
        object? navValue,
        INavigation nav,
        IStateManager stateManager)
    {
        var elements = new List<AttributeValue>();
        if (navValue is not IEnumerable collection)
            return elements;

        foreach (var element in collection)
        {
            if (element is null)
                continue;
            var ownedEntry = stateManager.TryGetEntry(element, nav.TargetEntityType);
            if (ownedEntry is null || ownedEntry.EntityState == EntityState.Deleted)
                continue;
            elements.Add(
                new AttributeValue { M = serializerSource.BuildItemFromOwnedEntry(ownedEntry) });
        }

        return elements;
    }

    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="nav" /> has at least one mutating
    ///     owned entry under <paramref name="principalEntry" /> in the current batch.
    /// </summary>
    /// <remarks>
    ///     O(1) — delegates to the pre-built <paramref name="mutatingNavs" /> lookup built once per
    ///     save call by <see cref="BuildMutatingNavLookup" />.
    /// </remarks>
    private static bool HasMutationForOwnedNavigation(
        INavigation nav,
        InternalEntityEntry principalEntry,
        Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
        => mutatingNavs.TryGetValue(principalEntry, out var set) && set.Contains(nav);

    /// <summary>
    ///     Pre-builds a lookup from each principal <see cref="InternalEntityEntry" /> to the set of
    ///     owned navigations that have at least one Add/Modify/Delete mutation in the current batch.
    /// </summary>
    /// <remarks>
    ///     Built once per <c>SaveChangesAsync</c> call — O(entries × depth). Each subsequent
    ///     <see cref="HasMutationForOwnedNavigation" /> check is then O(1) instead of O(entries × depth)
    ///     per navigation.
    /// </remarks>
    private static Dictionary<InternalEntityEntry, HashSet<INavigation>> BuildMutatingNavLookup(
        IList<IUpdateEntry> entries)
    {
        var lookup =
            new Dictionary<InternalEntityEntry, HashSet<INavigation>>(
                ReferenceEqualityComparer.Instance);

        foreach (var e in entries)
        {
            if (!e.EntityType.IsOwned()
                || e.EntityState is not EntityState.Modified
                    and not EntityState.Added
                    and not EntityState.Deleted)
                continue;

            // Walk the full ownership chain upward so that a mutation deep in the tree (e.g.
            // Root → Profile → Address → City) marks EVERY ancestor nav as mutating:
            //   lookup[Profile]   gets {AddressNav}
            //   lookup[Root]      gets {ProfileNav}
            // Without full propagation, Phase C at the root level would skip ProfileNav because
            // it only sees that Root has no direct owned-entry mutations — missing the deeper ones.
            var current = (InternalEntityEntry)e;
            while (current.EntityType.IsOwned())
            {
                var ownership = current.EntityType.FindOwnership();
                if (ownership is null)
                    break;

                var principal = current.StateManager.FindPrincipal(current, ownership);
                if (principal is null)
                    break;

                var nav = ownership.PrincipalToDependent;
                if (nav is null)
                    break;

                if (!lookup.TryGetValue(principal, out var set))
                {
                    set = new HashSet<INavigation>(ReferenceEqualityComparer.Instance);
                    lookup[principal] = set;
                }

                set.Add(nav);
                current = principal;
            }
        }

        return lookup;
    }

    /// <summary>
    ///     Builds a PartiQL <c>UPDATE</c> statement for an aggregate root that is in the
    ///     <see cref="EntityState.Unchanged" /> state but whose owned sub-entities have mutations.
    /// </summary>
    /// <remarks>
    ///     Skips phases A and B (scalar/collection root properties) because the root itself has no
    ///     modified properties — only phases C and D (owned navigation clauses) are run.
    /// </remarks>
    /// <param name="entry">The Unchanged aggregate root entry.</param>
    /// <param name="mutatingNavs">
    ///     Pre-built lookup from principal entry → set of owned navigations with
    ///     mutations.
    /// </param>
    /// <returns>
    ///     A tuple of (tableName, sql, parameters) when there is something to write, or
    ///     <see langword="null" /> when no owned sub-entities have effective changes.
    /// </returns>
    private (string tableName, string sql, List<AttributeValue> parameters)?
        BuildOwnedMutationUpdateStatement(
            IUpdateEntry entry,
            Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
        var entityType = entry.EntityType;
        var rootEntry = (InternalEntityEntry)entry;
        var stateManager = rootEntry.StateManager;

        var setClauses = new List<string>();
        var setParameters = new List<AttributeValue>();
        var removeClauses = new List<string>();
        var whereParameters = new List<AttributeValue>();

        // Phase C — OwnsOne navigations (same logic as in BuildModifiedUpdateStatement)
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

        // Phase D — OwnsMany navigations (same logic as in BuildModifiedUpdateStatement)
        foreach (var nav in entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned() && n.IsCollection))
        {
            if (!HasMutationForOwnedNavigation(nav, rootEntry, mutatingNavs))
                continue;

            var navAttrName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;
            var elements = BuildOwnedManyElements(entry.GetCurrentValue(nav), nav, stateManager);

            setClauses.Add($"\"{EscapeIdentifier(navAttrName)}\" = ?");
            setParameters.Add(new AttributeValue { L = elements });
        }

        if (setClauses.Count == 0 && removeClauses.Count == 0)
            return null;

        // WHERE: key + concurrency tokens (using original values)
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
    ///     Returns <see langword="true" /> if <paramref name="property" /> maps to a scalar DynamoDB
    ///     attribute — i.e. it is not a collection type (M/L/SS/NS/BS).
    /// </summary>
    /// <remarks>
    ///     Collection-typed properties are handled separately with full attribute replacement.
    ///     <c>byte[]</c> is a binary scalar (B attribute) and is explicitly allowed here.
    /// </remarks>
    private static bool IsScalarModifiedProperty(IProperty property)
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
    ///     Builds the set of root entries to process in the write loop.
    /// </summary>
    /// <remarks>
    ///     Includes all mutating non-owned roots (Added/Modified/Deleted). Also includes aggregate
    ///     roots for mutating owned entries even when those roots are currently
    ///     <see cref="EntityState.Unchanged" /> — such roots need an UPDATE statement that covers
    ///     the owned sub-graph changes. This is a pure projection and does not mutate tracker state.
    /// </remarks>
    private static List<IUpdateEntry> BuildRootEntries(IList<IUpdateEntry> entries)
    {
        var rootEntries = entries
            .Where(static e
                => !e.EntityType.IsOwned()
                && (e.EntityState == EntityState.Added
                    || e.EntityState == EntityState.Modified
                    || e.EntityState == EntityState.Deleted))
            .ToList();

        IncludeMutatingOwnedRoots(entries, rootEntries);
        return rootEntries;
    }

    /// <summary>
    ///     Ensures aggregate roots for mutating owned entries are present in
    ///     <paramref name="rootEntries" />.
    /// </summary>
    /// <remarks>
    ///     EF Core can track owned entry mutations independently from the root state. For owned-only
    ///     mutations, the root may remain <see cref="EntityState.Unchanged" /> and otherwise be absent
    ///     from the write loop. Injected Unchanged roots are handled by the <c>Unchanged</c> case in
    ///     the write switch alongside <c>Modified</c>; see that case for why state promotion is
    ///     intentionally avoided here.
    /// </remarks>
    private static void IncludeMutatingOwnedRoots(
        IList<IUpdateEntry> entries,
        List<IUpdateEntry> rootEntries)
    {
        var count = entries.Count;
        var trackedEntries = new HashSet<InternalEntityEntry>(ReferenceEqualityComparer.Instance);

        foreach (var rootEntry in rootEntries)
            trackedEntries.Add((InternalEntityEntry)rootEntry);

        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            if (!entry.EntityType.IsOwned()
                || entry.EntityState is not EntityState.Modified
                    and not EntityState.Added
                    and not EntityState.Deleted)
                continue;

            var root = GetRootEntry((InternalEntityEntry)entry);

            if (trackedEntries.Add(root))
                rootEntries.Add(root);
        }
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
