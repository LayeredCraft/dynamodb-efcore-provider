using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry.SetProperty

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoSaveChangesPlanner(
    DynamoEntityItemSerializerSource serializerSource,
    DynamoPartiqlStatementFactory statementFactory)
{
    internal const int MaxPartiQlStatementLength = 8192;

    public DynamoWritePlan Plan(IList<IUpdateEntry> entries)
    {
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
        var mutatingNavs = BuildMutatingNavLookup(entries);
        var operations = BuildWriteOperations(rootEntries, mutatingNavs);

        return new DynamoWritePlan(entries, rootEntries, operations);
    }

    private List<CompiledWriteOperation> BuildWriteOperations(
        IReadOnlyList<IUpdateEntry> rootEntries,
        Dictionary<InternalEntityEntry, HashSet<INavigation>> mutatingNavs)
    {
        var operations = new List<CompiledWriteOperation>(rootEntries.Count);

        foreach (var entry in rootEntries)
            switch (entry.EntityState)
            {
                case EntityState.Added:
                {
                    var item = serializerSource.BuildItem(entry);
                    var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
                    var (sql, parameters) = statementFactory.BuildInsertStatement(tableName, item);

                    AddCompiledOperation(
                        operations,
                        entry,
                        EntityState.Added,
                        tableName,
                        sql,
                        parameters);
                    break;
                }

                case EntityState.Modified:
                {
                    var update = statementFactory.BuildModifiedUpdateStatement(entry, mutatingNavs);
                    if (update is null)
                        continue;

                    AddCompiledOperation(
                        operations,
                        entry,
                        EntityState.Modified,
                        update.Value.tableName,
                        update.Value.sql,
                        update.Value.parameters);
                    break;
                }

                case EntityState.Unchanged:
                {
                    var update =
                        statementFactory.BuildOwnedMutationUpdateStatement(entry, mutatingNavs);
                    if (update is null)
                        continue;

                    AddCompiledOperation(
                        operations,
                        entry,
                        EntityState.Modified,
                        update.Value.tableName,
                        update.Value.sql,
                        update.Value.parameters);
                    break;
                }

                case EntityState.Deleted:
                {
                    var delete = statementFactory.BuildDeleteStatement(entry);
                    AddCompiledOperation(
                        operations,
                        entry,
                        EntityState.Deleted,
                        delete.tableName,
                        delete.sql,
                        delete.parameters);
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"SaveChanges for EntityState.{entry.EntityState} is not handled "
                        + $"in the write loop for '{entry.EntityType.DisplayName()}'.");
            }

        return operations;
    }

    private void AddCompiledOperation(
        ICollection<CompiledWriteOperation> operations,
        IUpdateEntry entry,
        EntityState entityState,
        string tableName,
        string statement,
        List<AttributeValue> parameters)
    {
        ValidateStatementLength(statement);
        operations.Add(
            new CompiledWriteOperation(
                entry,
                entityState,
                tableName,
                statement,
                parameters,
                statementFactory.BuildTargetItemIdentity(entry, tableName)));
    }

    private static void ValidateStatementLength(string statement)
    {
        if (!ContainsNonAscii(statement))
        {
            if (statement.Length <= MaxPartiQlStatementLength)
                return;

            throw new InvalidOperationException(
                $"The generated PartiQL statement is {statement.Length} characters "
                + $"(ASCII-equivalent bytes), which exceeds DynamoDB's "
                + $"{MaxPartiQlStatementLength}-byte statement-size limit. "
                + "Consider reducing the number of mapped scalar properties or splitting the "
                + "write unit across multiple SaveChanges calls.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(statement);
        if (byteCount <= MaxPartiQlStatementLength)
            return;

        throw new InvalidOperationException(
            $"The generated PartiQL statement is {byteCount} UTF-8 bytes, which exceeds DynamoDB's "
            + $"{MaxPartiQlStatementLength}-byte statement-size limit. "
            + "Consider reducing the number of mapped scalar properties or splitting the "
            + "write unit across multiple SaveChanges calls.");
    }

    private static bool ContainsNonAscii(string value)
    {
        foreach (var ch in value)
            if (ch > 0x7F)
                return true;

        return false;
    }

    private static List<IUpdateEntry> BuildRootEntries(IList<IUpdateEntry> entries)
    {
        var rootEntries = entries
            .Where(static e
                => !e.EntityType.IsOwned()
                && e.EntityState is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToList();

        IncludeMutatingOwnedRoots(entries, rootEntries);
        return rootEntries;
    }

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

            if (principal.EntityType.IsOwned())
            {
                entry = principal;
                continue;
            }

            return principal;
        }
    }

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
}
