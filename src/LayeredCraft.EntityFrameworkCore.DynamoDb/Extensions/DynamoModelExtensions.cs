using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;

internal static class DynamoModelExtensions
{
    extension(IModel model)
    {
        /// <summary>Gets the canonical runtime DynamoDB table model attached to the EF model.</summary>
        internal DynamoRuntimeTableModel? GetDynamoRuntimeTableModel()
            => model.FindRuntimeAnnotation(DynamoAnnotationNames.RuntimeTableModel)?.Value
                as DynamoRuntimeTableModel;
    }

    extension(IReadOnlyModel model)
    {
        /// <summary>Enumerates non-owned root entity types in model order.</summary>
        internal IEnumerable<IReadOnlyEntityType> EnumerateRootEntityTypes()
            => model.GetEntityTypes()
                .Where(static entityType => !entityType.IsOwned()
                    && entityType.FindOwnership() is null
                    && entityType.BaseType is null);
    }

    extension(IReadOnlyEntityType entityType)
    {
        /// <summary>Resolves the mapped entity type that owns the DynamoDB table-name metadata.</summary>
        internal IReadOnlyEntityType ResolveTableMappedEntityType()
        {
            if (entityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value is string)
                return entityType;

            var current = entityType;
            while (current.BaseType is { } baseType)
            {
                current = baseType;
                if (current.FindAnnotation(DynamoAnnotationNames.TableName)?.Value is string)
                    return current;
            }

            return current;
        }

        /// <summary>Enumerates configured secondary indexes across the mapped hierarchy in deterministic order.</summary>
        internal IEnumerable<IReadOnlyIndex> EnumerateSecondaryIndexesInHierarchy()
            => entityType
                .GetDerivedTypesInclusive()
                .OrderBy(static currentEntityType => currentEntityType.Name, StringComparer.Ordinal)
                .SelectMany(static currentEntityType => currentEntityType.GetDeclaredIndexes())
                .Where(static index => index.GetSecondaryIndexKind() is not null)
                .OrderBy(static index => index.GetSecondaryIndexName() ?? index.Name ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static index => index.DeclaringEntityType.Name, StringComparer.Ordinal);

        /// <summary>Gets the effective table-group name used for shared-table DynamoDB metadata.</summary>
        internal string GetTableGroupName()
        {
            var mappedEntityType = entityType.ResolveTableMappedEntityType();

            return mappedEntityType.FindAnnotation(DynamoAnnotationNames.TableName)?.Value as string
                ?? mappedEntityType.ClrType.Name;
        }
    }
}
