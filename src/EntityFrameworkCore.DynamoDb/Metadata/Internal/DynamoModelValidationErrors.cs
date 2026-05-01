using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Centralizes provider-specific model validation exceptions.</summary>
internal static class DynamoModelValidationErrors
{
    /// <summary>Creates the provider error used when owned entity types are configured.</summary>
    public static InvalidOperationException
        OwnedEntityTypesNotSupported(IReadOnlyEntityType entityType)
        => new(
            $"Entity type '{entityType.DisplayName()}' is configured as an owned entity type, "
            + "which is not supported by the DynamoDB provider. Use EF Core complex types "
            + "instead: annotate the CLR type with [ComplexType] and replace OwnsOne/OwnsMany "
            + "fluent calls with ComplexProperty()/ComplexCollection().");

    /// <summary>
    ///     Creates the provider error used when a recursive complex-property containment cycle is
    ///     detected in the model.
    /// </summary>
    public static InvalidOperationException ComplexContainmentCycleDetected(
        string rootProperty,
        IEnumerable<string> cycleSegments)
        => new(
            "Complex property containment cycle detected starting at '"
            + rootProperty
            + "': "
            + string.Join(" -> ", cycleSegments)
            + ". Recursive complex containment is not supported by the DynamoDB provider. "
            + "Complex types must form an acyclic containment tree rooted at an entity.");
}
