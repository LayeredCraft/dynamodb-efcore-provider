using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query;

/// <summary>Represents the DynamoQueryCompilationContextFactory type.</summary>
public class DynamoQueryCompilationContextFactory(QueryCompilationContextDependencies dependencies)
    : IQueryCompilationContextFactory
{
    /// <summary>Provides functionality for this member.</summary>
    public QueryCompilationContext Create(bool async)
        => new DynamoQueryCompilationContext(dependencies, async);
}
