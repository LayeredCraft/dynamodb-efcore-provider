using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Configures DynamoDB-compatible value generation by convention.
/// </summary>
/// <remarks>
///     DynamoDB does not provide identity columns or integer key generators. Numeric and string keys are
///     application-assigned unless users explicitly configure a client-side generator. Guid keys retain
///     EF Core's default client-side generation behavior.
/// </remarks>
public sealed class DynamoValueGenerationConvention(
    ProviderConventionSetBuilderDependencies dependencies) : ValueGenerationConvention(dependencies)
{
    /// <summary>
    ///     Gets the conventional value-generation setting for <paramref name="property" />.
    /// </summary>
    /// <param name="property">Property being configured by convention.</param>
    /// <returns>
    ///     EF Core's default conventional value-generation setting for Guid properties; otherwise
    ///     <see langword="null" /> so DynamoDB-incompatible numeric and string key generation is suppressed.
    /// </returns>
    protected override ValueGenerated? GetValueGenerated(IConventionProperty property)
        => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(Guid)
            ? base.GetValueGenerated(property)
            : null;
}
