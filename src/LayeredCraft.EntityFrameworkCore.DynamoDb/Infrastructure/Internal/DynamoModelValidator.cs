using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>
///     Provider-specific model validator which relaxes EF Core's default primitive collection
///     list-shape requirement. DynamoDB supports set and map semantics, so primitive collections are
///     accepted when the CLR type can be treated as <see cref="ICollection{T}" />.
/// </summary>
public class DynamoModelValidator(ModelValidatorDependencies dependencies)
    : ModelValidator(dependencies)
{
    /// <summary>Validates primitive collection properties for the DynamoDB provider.</summary>
    /// <remarks>
    ///     EF Core's base validator enforces <c>IList&lt;T&gt;</c> for sealed primitive collection
    ///     CLR types. This provider allows sealed types implementing <c>ICollection&lt;T&gt;</c> so shapes
    ///     such as <c>HashSet&lt;T&gt;</c> and dictionary-backed primitive collections are valid.
    /// </remarks>
    protected override void ValidatePrimitiveCollections(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var entityType in model.GetEntityTypes())
            ValidateType(entityType);

        // Walk entity and nested complex types so collection validation is applied consistently
        // across the full model graph.
        static void ValidateType(ITypeBase typeBase)
        {
            foreach (var property in typeBase.GetDeclaredProperties())
            {
                if (property is not { IsPrimitiveCollection: true, ClrType.IsArray: false })
                    continue;

                var elementType = TryGetElementType(property.ClrType);
                if (elementType == null)
                    throw new InvalidOperationException(
                        $"The type '{property.ClrType.ShortDisplayName()}' cannot be used as a primitive collection because it does not expose an element type via IEnumerable<T>.");

                var collectionType = typeof(ICollection<>).MakeGenericType(elementType);
                if (property.ClrType.IsSealed && !collectionType.IsAssignableFrom(property.ClrType))
                    throw new InvalidOperationException(
                        $"The type '{property.ClrType.ShortDisplayName()}' cannot be used as a primitive collection because it does not implement '{collectionType.ShortDisplayName()}'.");
            }

            foreach (var complexProperty in typeBase.GetDeclaredComplexProperties())
                ValidateType(complexProperty.ComplexType);
        }

        /// <summary>
        /// Attempts to resolve the collection element type from array or <c>IEnumerable&lt;T&gt;</c>
        /// implementations.
        /// </summary>
        static Type? TryGetElementType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type clrType)
        {
            if (clrType.IsArray)
                return clrType.GetElementType();

            if (clrType.IsGenericType
                && clrType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return clrType.GetGenericArguments()[0];

            foreach (var implementedInterface in clrType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType
                    || implementedInterface.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                    continue;

                return implementedInterface.GetGenericArguments()[0];
            }

            return null;
        }
    }
}
