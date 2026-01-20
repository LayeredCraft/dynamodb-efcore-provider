using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryContextFactory : IQueryContextFactory
{
    public QueryContext Create() => throw new NotImplementedException();
}
