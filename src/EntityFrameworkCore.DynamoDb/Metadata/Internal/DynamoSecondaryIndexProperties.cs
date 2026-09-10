using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>
///     Guards <c>IReadOnlyIndex.Properties</c> reads against complex-type members. EF Core 11
///     widened that member's element type from <see cref="IReadOnlyProperty" /> to
///     <see cref="IReadOnlyPropertyBase" /> so indexes can traverse complex-type properties; this
///     provider's DynamoDB secondary-index keys must remain scalar. Used both at model-validation
///     time (<c>DynamoModelValidator</c>, the primary gate) and defensively wherever a compiled
///     model may reach index metadata without re-running validation in-process.
/// </summary>
internal static class DynamoSecondaryIndexProperties
{
    /// <summary>
    ///     Returns <paramref name="indexProperty" /> as a scalar <see cref="IReadOnlyProperty" />,
    ///     or throws when it is a complex-type member.
    /// </summary>
#if NET11_0
    public static IReadOnlyProperty AsScalar(
        string declaringEntityDisplayName,
        string indexDisplayName,
        IReadOnlyPropertyBase indexProperty,
        string keyRole)
    {
        if (indexProperty is IReadOnlyProperty scalarProperty)
            return scalarProperty;

        throw DynamoModelValidationErrors.SecondaryIndexKeyMemberNotScalar(
            declaringEntityDisplayName,
            indexDisplayName,
            indexProperty.Name,
            keyRole);
    }
#else
    public static IReadOnlyProperty AsScalar(
        string declaringEntityDisplayName,
        string indexDisplayName,
        IReadOnlyProperty indexProperty,
        string keyRole)
        => indexProperty; // EF10: IReadOnlyIndex.Properties is already IReadOnlyList<IReadOnlyProperty>.
#endif
}
