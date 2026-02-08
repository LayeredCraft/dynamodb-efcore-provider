using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;

public interface IDynamoDbContextOptionsBuilder
{
    DbContextOptionsBuilder OptionsBuilder { get; }
}

public class DynamoDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    : IDynamoDbContextOptionsBuilder
{
    public DbContextOptionsBuilder OptionsBuilder { get; } = optionsBuilder;

    public virtual DynamoDbContextOptionsBuilder AuthenticationRegion(string region)
        => WithOption(e => e.WithAuthenticationRegion(Check.NotNull(region)));

    public virtual DynamoDbContextOptionsBuilder ServiceUrl(string serviceUrl)
        => WithOption(e => e.WithServiceUrl(Check.NotNull(serviceUrl)));

    /// <summary>
    ///     Sets the default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    /// <param name="pageSize">The default page size. Must be positive if specified.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DefaultPageSize(int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        return WithOption(e => e.WithDefaultPageSize(pageSize));
    }

    protected virtual DynamoDbContextOptionsBuilder WithOption(
        Func<DynamoDbOptionsExtension, DynamoDbOptionsExtension> setAction)
    {
        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(
            setAction(
                OptionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>()
                ?? new DynamoDbOptionsExtension()));

        return this;
    }
}
