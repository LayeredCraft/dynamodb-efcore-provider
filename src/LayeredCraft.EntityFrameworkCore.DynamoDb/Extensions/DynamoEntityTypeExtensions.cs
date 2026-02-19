// ReSharper disable CheckNamespace

using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoEntityTypeExtensions
{
    extension(IMutableEntityType entityType)
    {
        /// <summary>Sets the DynamoDB table name for the root entity type.</summary>
        public void SetTableName(string name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.TableName,
                Check.NullButNotEmpty(name));

        /// <summary>Sets the containing top-level attribute name for an embedded owned entity type.</summary>
        public void SetContainingAttributeName(string? name)
            => entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.ContainingAttributeName,
                name.NullButNotEmpty());
    }

    extension(IReadOnlyEntityType entityType)
    {
        /// <summary>Gets the configured containing top-level attribute name for an embedded owned entity type.</summary>
        public string? GetContainingAttributeName()
        {
            var configuredName =
                entityType[DynamoAnnotationNames.ContainingAttributeName] as string;
            if (!string.IsNullOrWhiteSpace(configuredName))
                return configuredName;

            return entityType.FindOwnership()?.PrincipalToDependent?.Name;
        }
    }
}
