using System.Collections;
using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
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
    DynamoEntityItemSerializerSource serializerSource) : Database(dependencies)
{
    /// <summary>Synchronously saves changes by delegating to the async path.</summary>
    public override int SaveChanges(IList<IUpdateEntry> entries)
        => SaveChangesAsync(entries).GetAwaiter().GetResult();

    /// <summary>
    /// Persists all <see cref="EntityState.Added"/> entries as PartiQL <c>INSERT</c> statements.
    /// Modified and Deleted entries are not yet supported and will throw.
    /// </summary>
    /// <param name="entries">The tracked entity changes to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The number of root entities written.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when any entry has a state other than <see cref="EntityState.Added"/>.
    /// </exception>
    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        // Guard: only Added is implemented; fail explicitly for Modified/Deleted.
        var unsupported = entries.FirstOrDefault(static e
            => e.EntityState is not EntityState.Added && !e.EntityType.IsOwned());

        if (unsupported is not null)
            throw new NotSupportedException(
                $"SaveChanges for EntityState.{unsupported.EntityState} is not yet supported. "
                + "Only Added entities can be persisted in this version.");

        // Build a lookup from owned CLR entity object → its IUpdateEntry for all nesting depths.
        // ToEntityEntry().Entity is the canonical path from IUpdateEntry to the CLR object.
        var ownedEntries = entries
            .Where(static e => e.EntityType.IsOwned())
            .ToDictionary(e => e.ToEntityEntry().Entity, ReferenceEqualityComparer.Instance);

        var rootEntries = entries
            .Where(static e => !e.EntityType.IsOwned() && e.EntityState == EntityState.Added)
            .ToList();

        foreach (var entry in rootEntries)
        {
            // Serialization is handled by the compiled, per-entity-type serializer — no
            // per-call type dispatch or value-type boxing on the scalar-property hot path.
            var item = serializerSource.BuildItem(entry, ownedEntries);
            var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
            var (sql, parameters) = BuildInsertStatement(tableName, item);

            await clientWrapper
                .ExecuteWriteAsync(sql, parameters, cancellationToken)
                .ConfigureAwait(false);

            // Transition the root entity to Unchanged so EF Core knows it is persisted.
            entry.EntityState = EntityState.Unchanged;

            // Walk owned navigations and mark every owned sub-entry as Unchanged too.
            MarkOwnedEntriesUnchanged(entry, ownedEntries);
        }

        return rootEntries.Count;
    }

    /// <summary>
    /// Generates a PartiQL <c>INSERT INTO "table" VALUE {'key': ?, ...}</c> statement with
    /// one positional parameter per top-level attribute in <paramref name="item"/>.
    /// </summary>
    private static (string sql, List<AttributeValue> parameters) BuildInsertStatement(
        string tableName,
        Dictionary<string, AttributeValue> item)
    {
        var sql = new StringBuilder();
        sql.Append($"INSERT INTO \"{tableName}\" VALUE {{");

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

    /// <summary>
    /// Recursively marks all owned sub-entries reachable from <paramref name="ownerEntry"/>
    /// as <see cref="EntityState.Unchanged"/> after a successful write.
    /// </summary>
    private static void MarkOwnedEntriesUnchanged(
        IUpdateEntry ownerEntry,
        IReadOnlyDictionary<object, IUpdateEntry> ownedEntries)
    {
        foreach (var navigation in ownerEntry
            .EntityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned()))
        {
            var navigationValue = ownerEntry.GetCurrentValue(navigation);
            if (navigationValue is null)
                continue;

            if (navigation.IsCollection)
            {
                if (navigationValue is not IEnumerable collection)
                    continue;

                foreach (var element in collection)
                {
                    if (element is not null
                        && ownedEntries.TryGetValue(element, out var ownedEntry))
                    {
                        ownedEntry.EntityState = EntityState.Unchanged;
                        MarkOwnedEntriesUnchanged(ownedEntry, ownedEntries);
                    }
                }
            }
            else
            {
                if (ownedEntries.TryGetValue(navigationValue, out var ownedEntry))
                {
                    ownedEntry.EntityState = EntityState.Unchanged;
                    MarkOwnedEntriesUnchanged(ownedEntry, ownedEntries);
                }
            }
        }
    }
}
