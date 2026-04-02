using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Update.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Executes <see cref="SaveChanges" /> by compiling tracked entries to PartiQL write statements.</summary>
public class DynamoDatabaseWrapper(
    DatabaseDependencies dependencies,
    IDynamoClientWrapper clientWrapper,
    IDynamoWriteSqlGeneratorFactory writeSqlGeneratorFactory,
    ITypeMappingSource typeMappingSource) : Database(dependencies)
{
    /// <inheritdoc />
    public override int SaveChanges(IList<IUpdateEntry> entries)
        => SaveChangesAsync(entries).GetAwaiter().GetResult();

    /// <summary>
    ///     Persists all tracked <see cref="IUpdateEntry" /> instances by compiling each root entity
    ///     to a single PartiQL write statement and executing it via <see cref="IDynamoClientWrapper" />.
    /// </summary>
    /// <param name="entries">The tracked entries to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of root entities written.</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when an entry has an <see cref="EntityState" /> other than <see cref="EntityState.Added" />.
    ///     Modified and Deleted support is added in later stories.
    /// </exception>
    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var builder = new InsertExpressionBuilder(typeMappingSource);
        var count = 0;

        foreach (var entry in entries)
        {
            // Owned entity entries are handled as part of the root item write in story 6gu.2.
            // EF Core includes them as separate IUpdateEntry instances; skip them here.
            if (entry.EntityType.IsOwned())
                continue;

            switch (entry.EntityState)
            {
                case EntityState.Added:
                {
                    var plan = builder.Build(entry);
                    var generator = writeSqlGeneratorFactory.Create();
                    var query = generator.Generate(plan.Expression, plan.ParameterValues);

                    await clientWrapper
                        .ExecuteWriteStatementAsync(
                            new ExecuteStatementRequest
                            {
                                Statement = query.Sql, Parameters = [..query.Parameters],
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                    count++;
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"EntityState '{entry.EntityState}' is not yet supported by this provider. "
                        + "Only EntityState.Added is currently implemented.");
            }
        }

        return count;
    }
}
