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
}
