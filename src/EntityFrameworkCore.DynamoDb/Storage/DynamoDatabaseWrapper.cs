using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Represents the DynamoDatabaseWrapper type.</summary>
public class DynamoDatabaseWrapper(DatabaseDependencies dependencies) : Database(dependencies)
{
    /// <summary>Provides functionality for this member.</summary>
    public override int SaveChanges(IList<IUpdateEntry> entries)
        => throw new NotImplementedException();

    /// <summary>Provides functionality for this member.</summary>
    public override Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = new())
        => throw new NotImplementedException();
}
