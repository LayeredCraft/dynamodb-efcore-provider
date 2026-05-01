using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Rejects recursive complex-property containment before later conventions recurse the same
///     graph.
/// </summary>
public sealed class DynamoComplexContainmentValidationConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
        => DynamoComplexContainmentValidator.ValidateAcyclicContainment(modelBuilder.Metadata);
}
