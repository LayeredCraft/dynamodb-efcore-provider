using System.Collections;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ValueGeneration.Internal;

/// <summary>Generates a 1-based ordinal key for owned collection elements.</summary>
public sealed class OwnedOrdinalValueGenerator : ValueGenerator<int>
{
    /// <summary>Indicates the generated values are stable and non-temporary.</summary>
    public override bool GeneratesTemporaryValues => false;

    /// <summary>Computes the ordinal based on the element position in the owner collection.</summary>
    public override int Next(EntityEntry entry)
    {
        var entityType = entry.Metadata;
        var ownership = entityType.FindOwnership();

        if (ownership is null || ownership.IsUnique)
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' is not an owned collection element.");

        var dependentInternalEntry = ((IInfrastructure<InternalEntityEntry>)entry).Instance;
        var stateManager = entry.Context.GetService<IStateManager>();
        var principalEntry = stateManager.FindPrincipal(dependentInternalEntry, ownership);
        var principalToDependent = ownership.PrincipalToDependent;

        if (principalEntry is null || principalToDependent is null)
            return 0;

        if (principalEntry[principalToDependent] is not IEnumerable enumerable)
            return 0;

        var ordinal = 1;
        foreach (var element in enumerable)
        {
            if (ReferenceEquals(element, entry.Entity))
                return ordinal;

            ordinal++;
        }

        return 0;
    }
}
