using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

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
}

#pragma warning restore EF1001
