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

    public virtual DynamoDbContextOptionsBuilder AuthenticationRegion(string region) =>
        WithOption(e => e.WithAuthenticationRegion(Check.NotNull(region)));

    public virtual DynamoDbContextOptionsBuilder ServiceUrl(string serviceUrl) =>
        WithOption(e => e.WithServiceUrl(Check.NotNull(serviceUrl)));

    protected virtual DynamoDbContextOptionsBuilder WithOption(
        Func<DynamoDbOptionsExtension, DynamoDbOptionsExtension> setAction)
    {
        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(
            setAction(
                OptionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>() ??
                new DynamoDbOptionsExtension()));

        return this;
    }
}
