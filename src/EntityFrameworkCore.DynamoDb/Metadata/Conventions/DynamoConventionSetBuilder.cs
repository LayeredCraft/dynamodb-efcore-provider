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
        // Must run before DynamoKeyAnnotationConvention so PK/SK attribute names are already
        // transformed when key validation reads them via GetAttributeName().
        conventionSet.ModelFinalizingConventions.Add(new DynamoAttributeNamingConventionApplier());
        conventionSet.ModelFinalizingConventions.Add(new DynamoKeyAnnotationConvention());

        var keyInPrimaryKeyConvention = new DynamoKeyInPrimaryKeyConvention(Dependencies);
        conventionSet.EntityTypeAnnotationChangedConventions.Add(keyInPrimaryKeyConvention);
        conventionSet.PropertyAddedConventions.Add(keyInPrimaryKeyConvention);

        conventionSet.ModelFinalizingConventions.Add(new OwnedTypePrimaryKeyConvention());

        conventionSet.EntityTypeAddedConventions.Add(new DynamoResponseShadowPropertyConvention());

        return conventionSet;
    }
}
