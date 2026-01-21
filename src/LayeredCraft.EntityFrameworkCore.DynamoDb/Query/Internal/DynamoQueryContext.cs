using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryContext(QueryContextDependencies dependencies)
    : QueryContext(dependencies) { }
