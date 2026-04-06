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

        var rootEntries = entries
            .Where(static e => !e.EntityType.IsOwned() && e.EntityState == EntityState.Added)
            .ToList();

        foreach (var entry in rootEntries)
        {
            // Serialization is handled by the compiled, per-entity-type serializer — no
            // per-call type dispatch or value-type boxing on the scalar-property hot path.
            // Owned sub-entries are resolved on-demand via the EF state manager.
            var item = serializerSource.BuildItem(entry);
            var tableName = (string)entry.EntityType[DynamoAnnotationNames.TableName]!;
            var (sql, parameters) = BuildInsertStatement(tableName, item);

            await clientWrapper
                .ExecuteWriteAsync(sql, parameters, cancellationToken)
                .ConfigureAwait(false);
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
}
