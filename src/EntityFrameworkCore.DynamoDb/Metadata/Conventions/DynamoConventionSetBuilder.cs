using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Builds the EF Core convention set for the DynamoDB provider.</summary>
public sealed class DynamoConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies)
    : ProviderConventionSetBuilder(dependencies)
{
    /// <summary>Adds DynamoDB provider conventions to the model-building pipeline.</summary>
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        conventionSet.Remove(typeof(ForeignKeyIndexConvention));
        conventionSet.Replace<RelationshipDiscoveryConvention>(
            new DynamoRelationshipDiscoveryConvention(Dependencies));
        conventionSet.Replace<KeyDiscoveryConvention>(
            new DynamoKeyDiscoveryConvention(Dependencies));
        conventionSet.ModelFinalizingConventions.Add(new DynamoDiscriminatorConvention());
        conventionSet.ModelFinalizingConventions.Add(new DynamoVersionConvention());
        conventionSet.ModelFinalizingConventions.Add(new DynamoKeyAnnotationConvention());

        var keyInPrimaryKeyConvention = new DynamoKeyInPrimaryKeyConvention(Dependencies);
        conventionSet.EntityTypeAnnotationChangedConventions.Add(keyInPrimaryKeyConvention);
        conventionSet.PropertyAddedConventions.Add(keyInPrimaryKeyConvention);

        conventionSet.ModelFinalizingConventions.Add(new OwnedTypePrimaryKeyConvention());

        return conventionSet;
    }
}
