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

        // Owned entity types and foreign-key entity relationships are not supported.
        // Remove [Owned] processing and the base relationship-discovery convention so that
        // navigation properties to non-[ComplexType] types do not silently create spurious entity
        // relationships.
        conventionSet.Remove(typeof(OwnedAttributeConvention));
        conventionSet.Remove(typeof(RelationshipDiscoveryConvention));
        conventionSet.Remove(typeof(ForeignKeyIndexConvention));

        conventionSet.Replace<ComplexPropertyDiscoveryConvention>(
            new DynamoComplexPropertyDiscoveryConvention(Dependencies, true));

        conventionSet.Replace<ValueGenerationConvention>(
            new DynamoValueGenerationConvention(Dependencies));
        conventionSet.Replace<KeyDiscoveryConvention>(
            new DynamoKeyDiscoveryConvention(Dependencies));
        conventionSet.Replace<DiscriminatorConvention>(
            new DynamoDiscriminatorConvention(Dependencies));
        conventionSet.ModelFinalizingConventions.Add(
            new DynamoOwnedEntityTypeValidationConvention());

        var relationshipValidationConvention = new DynamoRelationshipValidationConvention();
        // Do not register ForeignKeyAddedConventions here: fixture customization can still ignore
        // relationships discovered by shared base models before model finalization.
        conventionSet.ForeignKeyOwnershipChangedConventions.Add(relationshipValidationConvention);
        conventionSet.ModelFinalizingConventions.Add(relationshipValidationConvention);

        conventionSet.ModelFinalizingConventions.Add(
            new DynamoComplexContainmentValidationConvention());
        // Must run before DynamoKeyAnnotationConvention so PK/SK attribute names are already
        // transformed when key validation reads them via GetAttributeName().
        conventionSet.ModelFinalizingConventions.Add(new DynamoAttributeNamingConventionApplier());
        conventionSet.ModelFinalizingConventions.Add(new DynamoKeyAnnotationConvention());

        var keyInPrimaryKeyConvention = new DynamoKeyInPrimaryKeyConvention(Dependencies);
        conventionSet.EntityTypeAnnotationChangedConventions.Add(keyInPrimaryKeyConvention);
        conventionSet.PropertyAddedConventions.Add(keyInPrimaryKeyConvention);

        conventionSet.EntityTypeAddedConventions.Add(new DynamoResponseShadowPropertyConvention());

        return conventionSet;
    }
}
