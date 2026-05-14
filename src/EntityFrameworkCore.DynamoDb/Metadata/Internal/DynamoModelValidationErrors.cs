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

    /// <summary>Creates the provider error used when foreign-key relationships are configured.</summary>
    public static InvalidOperationException ForeignKeyRelationshipsNotSupported(
        IReadOnlyForeignKey foreignKey)
    {
        var dependentEntityType = foreignKey.DeclaringEntityType.DisplayName();
        var principalEntityType = foreignKey.PrincipalEntityType.DisplayName();
        var foreignKeyProperties = foreignKey.Properties.Count == 0
            ? "<none>"
            : string.Join(", ", foreignKey.Properties.Select(static property => property.Name));
        var dependentNavigation = foreignKey.DependentToPrincipal?.Name ?? "<none>";
        var principalNavigation = foreignKey.PrincipalToDependent?.Name ?? "<none>";

        return new InvalidOperationException(
            $"Foreign key relationship from dependent entity type '{dependentEntityType}' "
            + $"to principal entity type '{principalEntityType}' is not supported by the "
            + $"DynamoDB provider. Foreign key properties: [{foreignKeyProperties}]. "
            + $"Dependent navigation: '{dependentNavigation}'. Principal navigation: "
            + $"'{principalNavigation}'. DynamoDB provider does not support "
            + "HasOne/HasMany/WithOne/WithMany/HasForeignKey or relational navigation "
            + "relationships. Use EF Core complex types ([ComplexType], "
            + "ComplexProperty(...), ComplexCollection(...)) for embedded data, or model "
            + "separate DynamoDB items/tables as separate root entities without EF "
            + "relationships.");
    }

    /// <summary>Creates the provider error used when unmapped CLR navigation properties are found.</summary>
    public static InvalidOperationException
        NavigationRelationshipsNotSupported(
            IReadOnlyEntityType entityType,
            string navigationName,
            Type targetType)
        => new(
            $"Navigation relationship '{entityType.DisplayName()}.{navigationName}' targeting "
            + $"'{targetType.Name}' is not supported by the DynamoDB provider. "
            + "DynamoDB provider does not support HasOne/HasMany/WithOne/WithMany/"
            + "HasForeignKey, [ForeignKey], [InverseProperty], or relational navigation "
            + "relationships. Use EF Core complex types ([ComplexType], "
            + "ComplexProperty(...), ComplexCollection(...)) for embedded data, or model "
            + "separate DynamoDB items/tables as separate root entities without EF "
            + "relationships.");

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
