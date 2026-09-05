using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query;

/// <summary>Represents the DynamoQueryCompilationContextFactory type.</summary>
public sealed class DynamoQueryCompilationContextFactory(
    QueryCompilationContextDependencies dependencies) : IQueryCompilationContextFactory
{
    /// <summary>Provides functionality for this member.</summary>
    public QueryCompilationContext Create(bool async)
        => new DynamoQueryCompilationContext(dependencies, async);

    /// <summary>Creates a query compilation context for EF Core's precompiled-query generator.</summary>
#pragma warning disable EF9100
    public QueryCompilationContext CreatePrecompiled(bool async)
        => new DynamoQueryCompilationContext(dependencies, async, true);
#pragma warning restore EF9100
}
