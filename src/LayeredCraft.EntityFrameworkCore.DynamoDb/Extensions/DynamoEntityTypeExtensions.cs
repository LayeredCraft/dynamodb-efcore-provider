// ReSharper disable CheckNamespace

using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoEntityTypeExtensions
{
    extension(IMutableEntityType entityType)
    {
        public void SetTableName(string name) =>
            entityType.SetOrRemoveAnnotation(
                DynamoAnnotationNames.TableName,
                Check.NullButNotEmpty(name));
    }
}
