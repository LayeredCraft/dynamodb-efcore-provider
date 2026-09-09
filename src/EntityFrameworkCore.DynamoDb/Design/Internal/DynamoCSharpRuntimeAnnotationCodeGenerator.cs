using System.Reflection;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Design.Internal;

#pragma warning disable EF1001

/// <summary>Filters provider runtime annotations when generating a compiled model.</summary>
public sealed class DynamoCSharpRuntimeAnnotationCodeGenerator(
    CSharpRuntimeAnnotationCodeGeneratorDependencies dependencies)
    : CSharpRuntimeAnnotationCodeGenerator(dependencies)
{
    /// <inheritdoc />
    public override void Generate(
        IModel model,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        // DynamoModelRuntimeInitializer rebuilds this derived lookup from compiled metadata.
        parameters.Annotations.Remove(DynamoAnnotationNames.RuntimeTableModel);
        base.Generate(model, parameters);
    }

    /// <inheritdoc />
    public override bool Create(
        CoreTypeMapping typeMapping,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
        ValueComparer? valueComparer = null,
        ValueComparer? keyValueComparer = null,
        ValueComparer? providerValueComparer = null)
    {
        if (typeMapping is DynamoTypeMapping dynamoMapping)
        {
            // Property-level converters compose outside the collection codec, so the primed
            // collection shape below would be missed and the mapping emitted unprimed. Fail at
            // compiled-model generation instead of first query execution under NativeAOT.
            if (dynamoMapping.Converter is not null
                && DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(
                    dynamoMapping.ClrType))
                throw new NotSupportedException(
                    "Compiled-model DynamoDB collection mappings composed with a property-level "
                    + "value converter are not supported. Remove the converter (convert the "
                    + "collection elements instead) or avoid the compiled model for this context.");

            if (TryEmitPrimedCollectionMapping(dynamoMapping, parameters))
                return true;
        }

        return base.Create(
            typeMapping,
            parameters,
            valueComparer,
            keyValueComparer,
            providerValueComparer);
    }

    private bool TryEmitPrimedCollectionMapping(
        DynamoTypeMapping typeMapping,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
    {
        // The collection shape and generic arguments come from the mapping CLR type directly, so
        // generation never touches ReaderWriter — accessing it would lazily construct the dynamic
        // collection codec through MakeGenericMethod/Invoke, which is exactly the reflection the
        // primed codec exists to avoid. Unsupported collection shapes fail fast below instead of
        // shipping a mapping that would build its codec via reflection (and crash under NativeAOT).
        var clrType = typeMapping.ClrType;
        string primeMethodName;
        Type elementType;
        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
            (primeMethodName, elementType) =
                (nameof(DynamoGeneratedModelRuntime.PrimeListMapping), listElementType);
        else if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out _))
            (primeMethodName, elementType) =
                (nameof(DynamoGeneratedModelRuntime.PrimeDictionaryMapping), dictionaryValueType);
        else if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
            (primeMethodName, elementType) =
                (nameof(DynamoGeneratedModelRuntime.PrimeSetMapping), setElementType);
        else
            return ThrowOrSkipUnprimedCollection(typeMapping);

        var code = Dependencies.CSharpHelper;
        AddNamespace(typeof(DynamoGeneratedModelRuntime), parameters.Namespaces);
        AddNamespace(clrType, parameters.Namespaces);
        AddNamespace(elementType, parameters.Namespaces);

        parameters
            .MainBuilder
            .Append(code.Reference(typeof(DynamoGeneratedModelRuntime)))
            .Append('.')
            .Append(primeMethodName)
            .Append('<')
            .Append(code.Reference(clrType))
            .Append(", ")
            .Append(code.Reference(elementType))
            .Append(">((")
            .Append(code.Reference(typeof(DynamoTypeMapping)))
            .Append(")(");

        var created = base.Create(typeMapping, parameters);
        parameters.MainBuilder.Append("))");

        return created;
    }

    private static bool ThrowOrSkipUnprimedCollection(DynamoTypeMapping typeMapping)
    {
        if (!DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(typeMapping.ClrType))
            return false;

        throw new NotSupportedException(
            "Compiled-model generation could not emit a primed collection mapping for '"
            + $"{typeMapping.ClrType.Name}'. Unprimed collection mappings build their codecs "
            + "through reflection at runtime, which fails under NativeAOT. Use a supported "
            + "collection shape (List<T>, HashSet<T>, Dictionary<string, T>, or "
            + "ReadOnlyDictionary<string, T> of primitive elements), or avoid the compiled model "
            + "for this context.");
    }
}

#pragma warning restore EF1001
