using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoDatabaseWrapper(DatabaseDependencies dependencies) : Database(dependencies)
{
    public override int SaveChanges(IList<IUpdateEntry> entries) =>
        throw new NotImplementedException();

    public override Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = new()) =>
        throw new NotImplementedException();
}
