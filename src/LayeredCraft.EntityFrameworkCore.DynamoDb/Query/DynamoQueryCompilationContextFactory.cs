using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query;

public class DynamoQueryCompilationContextFactory(QueryCompilationContextDependencies dependencies)
    : IQueryCompilationContextFactory
{
    public QueryCompilationContext Create(bool async)
        => new DynamoQueryCompilationContext(dependencies, async);
}
