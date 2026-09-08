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
        var readerWriterType = typeMapping.ReaderWriter?.GetType();
        if (readerWriterType?.IsGenericType != true)
            return false;

        var genericDefinition = readerWriterType.GetGenericTypeDefinition();
        var primeMethodName =
            genericDefinition == typeof(ListDynamoValueReaderWriter<,>)
                ?
                nameof(DynamoGeneratedModelRuntime.PrimeListMapping)
                : genericDefinition == typeof(DictionaryDynamoValueReaderWriter<,>)
                    ? nameof(DynamoGeneratedModelRuntime.PrimeDictionaryMapping)
                    : genericDefinition == typeof(SetDynamoValueReaderWriter<,>)
                        ? nameof(DynamoGeneratedModelRuntime.PrimeSetMapping)
                        : null;
        if (primeMethodName is null || typeMapping.ClrType == typeof(object))
            return false;

        var genericArguments = readerWriterType.GetGenericArguments();
        var code = Dependencies.CSharpHelper;
        AddNamespace(typeof(DynamoGeneratedModelRuntime), parameters.Namespaces);
        AddNamespace(genericArguments[0], parameters.Namespaces);
        AddNamespace(genericArguments[1], parameters.Namespaces);

        parameters
            .MainBuilder
            .Append(code.Reference(typeof(DynamoGeneratedModelRuntime)))
            .Append('.')
            .Append(primeMethodName)
            .Append('<')
            .Append(code.Reference(genericArguments[0]))
            .Append(", ")
            .Append(code.Reference(genericArguments[1]))
            .Append(">((")
            .Append(code.Reference(typeof(DynamoTypeMapping)))
            .Append(")(");

        var created = base.Create(typeMapping, parameters);
        parameters.MainBuilder.Append("))");

        return created;
    }
}

#pragma warning restore EF1001
