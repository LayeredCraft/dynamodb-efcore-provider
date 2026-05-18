using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Provides cached method metadata for DynamoDB queryable extension markers.</summary>
internal static class DynamoQueryableMethods
{
#pragma warning disable EF9102
    public static readonly MethodInfo ToPageAsync =
        ((Func<IQueryable<object>, int, string?, CancellationToken, Task<DynamoPage<object>>>)
            DynamoDbQueryableExtensions.ToPageAsync<object>).Method.GetGenericMethodDefinition();
#pragma warning restore EF9102

    public static readonly MethodInfo WithNextToken =
        ((Func<IQueryable<object>, string, IQueryable<object>>)DynamoDbQueryableExtensions
            .WithNextToken<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo Limit =
        ((Func<IQueryable<object>, int, IQueryable<object>>)DynamoDbQueryableExtensions
            .Limit<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo WithConsistentRead =
        ((Func<IQueryable<object>, bool, IQueryable<object>>)DynamoDbQueryableExtensions
            .WithConsistentRead<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo WithoutIndex =
        ((Func<IQueryable<object>, IQueryable<object>>)DynamoDbQueryableExtensions
            .WithoutIndex<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo WithIndex =
        ((Func<IQueryable<object>, string, IQueryable<object>>)DynamoDbQueryableExtensions
            .WithIndex<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo AsUnsafeFilteredQuery =
        ((Func<IQueryable<object>, IQueryable<object>>)DynamoDbQueryableExtensions
            .AsUnsafeFilteredQuery<object>).Method.GetGenericMethodDefinition();

    public static readonly MethodInfo AllowScan =
        ((Func<IQueryable<object>, IQueryable<object>>)DynamoDbQueryableExtensions
            .AllowScan<object>).Method.GetGenericMethodDefinition();
}
