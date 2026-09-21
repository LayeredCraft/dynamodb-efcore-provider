using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Provider options that singleton services (such as the model initializer) depend on.</summary>
/// <remarks>
///     Provider infrastructure, not a consumer contract. It is public because
///     <see cref="DynamoModelRuntimeInitializer" />'s public constructor, which EF's dependency
///     injection requires, takes it; EF's own providers expose their singleton options the same way.
///     Initialized once per EF internal service provider. Anything exposed here must be part of
///     <see cref="DynamoDbOptionsExtension.DynamoOptionsExtensionInfo" />'s service-provider
///     hash and equality so that a differently configured context never shares these values.
/// </remarks>
public interface IDynamoSingletonOptions : ISingletonOptions
{
    /// <summary>The runtime physical resource names, or <see langword="null" /> when none are configured.</summary>
    DynamoRuntimeResourceNames? RuntimeResourceNames { get; }
}

/// <summary>Default <see cref="IDynamoSingletonOptions" /> implementation.</summary>
internal sealed class DynamoSingletonOptions : IDynamoSingletonOptions
{
    /// <inheritdoc />
    public DynamoRuntimeResourceNames? RuntimeResourceNames { get; private set; }

    /// <inheritdoc />
    public void Initialize(IDbContextOptions options)
        => RuntimeResourceNames = options
            .FindExtension<DynamoDbOptionsExtension>()
            ?.RuntimeResourceNames;

    /// <remarks>
    ///     Intentionally does nothing. The runtime resource names are part of
    ///     <see cref="DynamoDbOptionsExtension.DynamoOptionsExtensionInfo" />'s service-provider hash
    ///     and equality, so a differently configured context never reaches this instance. A compiled
    ///     model shared between differently configured contexts is rejected by
    ///     <see cref="DynamoModelRuntimeInitializer" />.
    /// </remarks>
    public void Validate(IDbContextOptions options)
    {
    }
}
