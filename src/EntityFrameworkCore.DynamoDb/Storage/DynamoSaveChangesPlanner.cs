using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoSaveChangesPlanner(
    DynamoEntityItemSerializerSource serializerSource,
    DynamoPartiqlStatementFactory statementFactory)
{
    public DynamoWritePlan Plan(IList<IUpdateEntry> entries)
    {
        var unsupported = entries.FirstOrDefault(static e
            => e.EntityState is not EntityState.Added
                and not EntityState.Modified
                and not EntityState.Deleted);

        if (unsupported is not null)
            throw new NotSupportedException(
                $"SaveChanges for EntityState.{unsupported.EntityState} is not yet supported. "
                + "Only Added, Modified, and Deleted entities can be persisted in this version.");

        var rootEntries = entries
            .Where(static e => e.EntityState is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .ToList();

        var operations = BuildWriteOperations(rootEntries);

        return new DynamoWritePlan(entries, rootEntries, operations);
    }

    private List<CompiledWriteOperation> BuildWriteOperations(
        IReadOnlyList<IUpdateEntry> rootEntries)
    {
        var operations = new List<CompiledWriteOperation>(rootEntries.Count);

        foreach (var entry in rootEntries)
            switch (entry.EntityState)
            {
                case EntityState.Added:
                {
                    var item = serializerSource.BuildItem(entry);
                    var tableName = entry.EntityType.GetTableGroupName();

                    // DynamoDB rejects { NULL: true } for GSI key attributes via PartiQL INSERT.
                    // Sparse GSIs require these attributes to simply be absent when not applicable.
                    foreach (var index in entry.EntityType.GetIndexes())
                    {
                        if (index.GetSecondaryIndexKind() is null)
                            continue;
                        foreach (var property in index.Properties)
                        {
                            var attrName = property.GetAttributeName();
                            if (item.TryGetValue(attrName, out var val) && val.NULL == true)
                                item.Remove(attrName);
                        }
                    }

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
                    var update = statementFactory.BuildModifiedUpdateStatement(entry);
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
        DynamoPartiQlStatementValidator.ValidateStatementLength(statement, "write");
        operations.Add(
            new CompiledWriteOperation(entry, entityState, tableName, statement, parameters));
    }
}
