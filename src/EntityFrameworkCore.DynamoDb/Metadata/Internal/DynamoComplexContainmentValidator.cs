using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Validates complex-type containment graphs and rejects recursive containment cycles.</summary>
internal static class DynamoComplexContainmentValidator
{
    /// <summary>Throws when the model contains a recursive complex-property containment cycle.</summary>
    public static void ValidateAcyclicContainment(IReadOnlyModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
            ValidateTypeBase(entityType);
    }

    /// <summary>Validates declared complex properties on the given type base and nested complex types.</summary>
    private static void ValidateTypeBase(IReadOnlyTypeBase typeBase)
    {
        var chain = new List<IReadOnlyComplexProperty>();

        Traverse(typeBase, chain);
    }

    /// <summary>Depth-first walks complex containment while tracking the active path to detect cycles.</summary>
    private static void Traverse(IReadOnlyTypeBase typeBase, List<IReadOnlyComplexProperty> chain)
    {
        foreach (var complexProperty in typeBase.GetDeclaredComplexProperties())
        {
            var complexType = complexProperty.ComplexType;
            var existingIndex = FindComplexTypeIndex(chain, complexType);
            if (existingIndex >= 0)
                throw CreateCycleException(chain, existingIndex, complexProperty);

            chain.Add(complexProperty);

            Traverse(complexType, chain);

            chain.RemoveAt(chain.Count - 1);
        }
    }

    /// <summary>Finds the index of a complex type in the active containment chain.</summary>
    private static int FindComplexTypeIndex(
        IReadOnlyList<IReadOnlyComplexProperty> chain,
        IReadOnlyComplexType complexType)
    {
        for (var i = 0; i < chain.Count; i++)
            if (ReferenceEquals(chain[i].ComplexType, complexType))
                return i;

        return -1;
    }

    /// <summary>Creates a user-facing cycle error describing the recursive containment path.</summary>
    private static InvalidOperationException CreateCycleException(
        IReadOnlyList<IReadOnlyComplexProperty> chain,
        int cycleStartIndex,
        IReadOnlyComplexProperty repeatingProperty)
    {
        var segments = chain.Skip(cycleStartIndex).Select(static cp => cp.Name).ToList();
        segments.Add(repeatingProperty.Name);

        var rootProperty = chain.Count == 0
            ? repeatingProperty.Name
            : chain[0].DeclaringType.DisplayName() + "." + chain[0].Name;

        return new InvalidOperationException(
            "Complex property containment cycle detected starting at '"
            + rootProperty
            + "': "
            + string.Join(" -> ", segments)
            + ". Recursive complex containment is not supported by the DynamoDB provider. "
            + "Complex types must form an acyclic containment tree rooted at an entity.");
    }
}
