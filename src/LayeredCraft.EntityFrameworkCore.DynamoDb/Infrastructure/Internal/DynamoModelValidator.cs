using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

internal sealed class DynamoModelValidator(ModelValidatorDependencies dependencies)
    : ModelValidator(dependencies)
{
    /// <summary>Validates primitive collection properties against DynamoDB provider shape constraints.</summary>
    protected override void ValidatePrimitiveCollections(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.ValidatePrimitiveCollections(model, logger);

        foreach (var entityType in model.GetEntityTypes())
        {
            ValidateTypeBase(entityType);

            foreach (var complexProperty in entityType.GetDeclaredComplexProperties())
                ValidateComplexType(complexProperty.ComplexType);
        }
    }

    /// <summary>Recursively validates primitive collection properties on a complex type graph.</summary>
    private static void ValidateComplexType(IComplexType complexType)
    {
        ValidateTypeBase(complexType);

        foreach (var complexProperty in complexType.GetDeclaredComplexProperties())
            ValidateComplexType(complexProperty.ComplexType);
    }

    /// <summary>Validates primitive collection properties declared directly on a type base.</summary>
    private static void ValidateTypeBase(ITypeBase typeBase)
    {
        foreach (var property in typeBase.GetDeclaredProperties())
        {
            if (!property.IsPrimitiveCollection)
                continue;

            if (DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(property.ClrType))
                continue;

            throw new InvalidOperationException(
                $"Property '{typeBase.DisplayName()}.{property.Name}' uses primitive collection CLR type "
                + $"'{property.ClrType.Name}', which is not supported by the DynamoDB provider. "
                + "Supported list shapes: T[], List<T>, IList<T>, IReadOnlyList<T>. "
                + "Supported set shapes: HashSet<T>, ISet<T>, IReadOnlySet<T>. "
                + "Supported dictionary shapes: Dictionary<string,TValue>, IDictionary<string,TValue>, "
                + "IReadOnlyDictionary<string,TValue>, ReadOnlyDictionary<string,TValue>. "
                + "Custom or derived concrete collection types are not supported.");
        }
    }
}
