using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryContextFactory(QueryContextDependencies dependencies) : IQueryContextFactory
{
    public QueryContext Create() => new DynamoQueryContext(dependencies);
}
