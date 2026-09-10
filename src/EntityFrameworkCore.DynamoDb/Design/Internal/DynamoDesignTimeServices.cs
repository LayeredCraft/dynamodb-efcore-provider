using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Query;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.Extensions.DependencyInjection;

[assembly:
    DesignTimeProviderServices(
        "EntityFrameworkCore.DynamoDb.Design.Internal.DynamoDesignTimeServices")]

namespace EntityFrameworkCore.DynamoDb.Design.Internal;

/// <summary>Registers DynamoDB provider services used by EF Core design-time tooling.</summary>
public sealed class DynamoDesignTimeServices : IDesignTimeServices
{
    /// <inheritdoc />
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddEntityFrameworkDynamo();

#pragma warning disable EF9100
        serviceCollection
            .AddSingleton<IPrecompiledQueryCodeGenerator, DynamoPrecompiledQueryCodeGenerator>()
            .AddSingleton<ICSharpRuntimeAnnotationCodeGenerator,
                DynamoCSharpRuntimeAnnotationCodeGenerator>();
#pragma warning restore EF9100

#pragma warning disable EF1001
        new EntityFrameworkDesignServicesBuilder(serviceCollection).TryAddCoreServices();
#pragma warning restore EF1001
    }
}
