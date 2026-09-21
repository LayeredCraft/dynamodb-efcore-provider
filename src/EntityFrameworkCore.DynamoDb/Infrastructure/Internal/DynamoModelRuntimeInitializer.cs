using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Builds runtime DynamoDB table metadata after model validation completes.</summary>
public sealed class DynamoModelRuntimeInitializer(
    ModelRuntimeInitializerDependencies dependencies,
    IDynamoSingletonOptions singletonOptions)
    : ModelRuntimeInitializer(dependencies)
{
    /// <summary>
    ///     Initializes the model and verifies that it was initialized with the same runtime
    ///     resource-name configuration as the calling context.
    /// </summary>
    /// <remarks>
    ///     EF initializes a model instance once, for the first internal service provider that sees
    ///     it. A compiled model instance is a process-wide singleton, so a second, differently
    ///     configured context would otherwise silently observe the first configuration's names.
    /// </remarks>
    public override IModel Initialize(
        IModel model,
        bool designTime = true,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation>? validationLogger = null)
    {
        var initialized = base.Initialize(model, designTime, validationLogger);

        var applied = model
            .FindRuntimeAnnotation(DynamoAnnotationNames.AppliedRuntimeResourceNames)
            ?.Value;
        if (!Equals(applied, singletonOptions.RuntimeResourceNames))
            throw new InvalidOperationException(
                "This model was already initialized with a different DynamoDB runtime resource-name "
                + "configuration. Runtime physical table and index names are fixed for the lifetime "
                + "of an initialized model, and a compiled model instance cannot be shared between "
                + "contexts configured with different runtime resource names.");

        return initialized;
    }

    /// <summary>Attaches the canonical runtime table model to the finalized runtime model.</summary>
    /// <remarks>
    ///     Overrides <c>ModelRuntimeInitializer.InitializeModel</c> rather than
    ///     <c>ModelRuntimeInitializer.Initialize</c> to avoid depending on the undocumented
    ///     return-value contract of <c>Initialize</c> changing based on <paramref name="designTime"/>.
    ///     The <c>prevalidation=false</c> pass runs after model validation, so all metadata is
    ///     guaranteed to be consistent when the runtime descriptors are built.
    /// </remarks>
    protected override void InitializeModel(IModel model, bool designTime, bool prevalidation)
    {
        base.InitializeModel(model, designTime, prevalidation);

        if (prevalidation)
            return;

        ApplyResourceNames(model, singletonOptions.RuntimeResourceNames);

        // Runtime table descriptors are built eagerly here, mirroring EF Core's relational
        // runtime model pattern: building also validates shared-table/index consistency, so
        // invalid models must fail during initialization, not at first query. The runtime
        // table-group name annotations act as a cache for per-entry SaveChanges lookups.
        model.GetOrAddRuntimeAnnotationValue(
            DynamoAnnotationNames.RuntimeTableModel,
            static currentModel => BuildRuntimeTableModel((IReadOnlyModel)currentModel!),
            model);
    }

    /// <summary>
    ///     Stores effective table-group and secondary-index names on runtime metadata, applying any
    ///     configured runtime physical names over the names configured in the model.
    /// </summary>
    /// <remarks>
    ///     Runs once per model instance (see <see cref="ModelRuntimeInitializer" />), before the
    ///     runtime table model is built and before any query template can cache a physical name.
    ///     Names are written as runtime annotations only, so the compiled model itself is never
    ///     modified and its design-time names remain what it was generated with.
    /// </remarks>
    private static void ApplyResourceNames(IModel model, DynamoRuntimeResourceNames? names)
    {
        if (names is null)
        {
            // No runtime mapping: the effective name is the name configured in the model.
            foreach (var entityType in model.GetEntityTypes())
                entityType.SetRuntimeAnnotation(
                    DynamoAnnotationNames.TableGroupName,
                    entityType.ComputeTableGroupName());

            return;
        }

        var groups = DynamoTableGroups.Resolve(model);
        var tableNames = ApplyTableNames(groups, names);

        foreach (var entityType in model.GetEntityTypes())
        {
            var modelName = entityType.ComputeTableGroupName();
            entityType.SetRuntimeAnnotation(
                DynamoAnnotationNames.TableGroupName,
                tableNames.GetValueOrDefault(modelName) ?? modelName);
        }

        ApplyIndexNames(groups, names);
        model.SetRuntimeAnnotation(DynamoAnnotationNames.AppliedRuntimeResourceNames, names);
    }

    /// <summary>Resolves the runtime physical table name of each mapped table group.</summary>
    /// <returns>The physical names keyed by the table's model (design-time) name.</returns>
    private static Dictionary<string, string> ApplyTableNames(
        IReadOnlyList<DynamoTableGroup> groups,
        DynamoRuntimeResourceNames names)
    {
        Dictionary<string, string> resolved = new(StringComparer.Ordinal);

        foreach (var (logicalTable, physicalName) in names.Tables)
        {
            var group = FindGroup(groups, logicalTable, "table");
            resolved[group.ModelName] = physicalName;
        }

        // Index mappings must also refer to a declared logical table, even when the table itself is
        // not remapped.
        foreach (var logicalTable in names.SecondaryIndexes.Keys)
            _ = FindGroup(groups, logicalTable, "secondary index");

        // A physical table is exactly one table group, so two tables must not resolve to one name.
        foreach (var collision in groups
            .GroupBy(
                group => resolved.GetValueOrDefault(group.ModelName) ?? group.ModelName,
                StringComparer.Ordinal)
            .Where(static effective => effective.Count() > 1))
            throw new InvalidOperationException(
                $"The DynamoDB runtime resource-name configuration resolves more than one table to the physical table '{collision.Key}': "
                + string.Join(", ", collision.Select(static group => $"'{group.LogicalName ?? group.ModelName}'"))
                + ". Each table needs its own physical name.");

        return resolved;
    }

    /// <summary>Applies runtime physical secondary-index names as runtime annotations.</summary>
    /// <remarks>
    ///     The logical index identity is the EF index name. Every secondary index with that name in
    ///     the table receives the physical name, matching how entity types sharing a table share the
    ///     physical index.
    /// </remarks>
    private static void ApplyIndexNames(
        IReadOnlyList<DynamoTableGroup> groups,
        DynamoRuntimeResourceNames names)
    {
        foreach (var (logicalTable, indexNames) in names.SecondaryIndexes)
        {
            var group = FindGroup(groups, logicalTable, "secondary index");
            var secondaryIndexes = group
                .EntityTypes
                .SelectMany(static entityType => entityType.GetDeclaredIndexes())
                .Where(static index => index.GetSecondaryIndexKind() is not null
                    && index.Name is not null)
                .ToArray();
            var indexesByName = secondaryIndexes.ToLookup(
                static index => index.Name!,
                StringComparer.Ordinal);

            foreach (var logicalIndex in indexNames.Keys)
                if (!indexesByName.Contains(logicalIndex))
                    throw new InvalidOperationException(
                        $"The DynamoDB runtime resource-name configuration maps secondary index '{logicalIndex}' of logical table '{logicalTable}', "
                        + "but the model declares no such index. "
                        + (indexesByName.Count == 0
                            ? "The table has no secondary indexes."
                            : $"Declared secondary indexes: {string.Join(", ", indexesByName.Select(static i => $"'{i.Key}'").Order(StringComparer.Ordinal))}.")
                        + " The logical index name is the EF index name passed to HasGlobalSecondaryIndex or HasLocalSecondaryIndex.");

            ValidateEffectiveIndexes(logicalTable, secondaryIndexes, indexNames);

            foreach (var (logicalIndex, physicalName) in indexNames)
                // The initializer only ever runs over an IModel, whose indexes are IIndex.
                foreach (var index in indexesByName[logicalIndex].Cast<IIndex>())
                    index.SetRuntimeAnnotation(
                        DynamoAnnotationNames.RuntimeSecondaryIndexName,
                        physicalName);
        }
    }

    /// <summary>
    ///     Checks the effective physical index names of a whole table, not only the indexes a mapping
    ///     mentions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The provider identifies one physical index by its configured (design-time) name: entity
    ///         types that share a table and declare an index with the same physical name share that one
    ///         index, and may give the EF index different names. A runtime mapping must not change that
    ///         identity, so two rules hold for every index of the table:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             indexes that the model declares with different physical names must not resolve to the
    ///             same effective name, whether or not the mapping mentions all of them;
    ///         </item>
    ///         <item>
    ///             indexes that the model declares with the same physical name must resolve to one
    ///             effective name, so a shared physical index is neither split nor partly mapped.
    ///         </item>
    ///     </list>
    ///     Equivalent declarations of one logical index on several entity types are the same resource
    ///     and are never treated as a conflict.
    /// </remarks>
    private static void ValidateEffectiveIndexes(
        string logicalTable,
        IReadOnlyList<IReadOnlyIndex> secondaryIndexes,
        IReadOnlyDictionary<string, string> indexNames)
    {
        var resources = secondaryIndexes
            .Select(index =>
            {
                var modelName = index[DynamoAnnotationNames.SecondaryIndexName] as string
                    ?? index.Name!;
                var mapped = indexNames.TryGetValue(index.Name!, out var runtimeName);
                return new EffectiveIndex(index.Name!, modelName, mapped, mapped ? runtimeName! : modelName);
            })
            .Distinct()
            .ToArray();

        foreach (var collision in resources
            .GroupBy(static resource => resource.EffectiveName, StringComparer.Ordinal)
            .Where(static effective => effective.Select(static r => r.ModelName).Distinct(StringComparer.Ordinal).Count() > 1))
            throw new InvalidOperationException(
                $"The DynamoDB runtime resource-name configuration resolves more than one secondary index of logical table '{logicalTable}' to the physical index '{collision.Key}': "
                + string.Join(", ", DescribeIndexes(collision))
                + ". Each index needs its own physical name; map the indexes to different names or map the other index as well.");

        foreach (var split in resources
            .GroupBy(static resource => resource.ModelName, StringComparer.Ordinal)
            .Where(static model => model.Select(static r => r.EffectiveName).Distinct(StringComparer.Ordinal).Count() > 1))
            throw new InvalidOperationException(
                $"The DynamoDB runtime resource-name configuration resolves the physical index '{split.Key}' of logical table '{logicalTable}', which the model shares between several logical indexes, to different names: "
                + string.Join(", ", DescribeIndexes(split))
                + ". Map every logical index that shares a physical index to the same physical name.");
    }

    private static IEnumerable<string> DescribeIndexes(IEnumerable<EffectiveIndex> resources)
        => resources
            .OrderByDescending(static resource => resource.Mapped)
            .ThenBy(static resource => resource.LogicalName, StringComparer.Ordinal)
            .Select(static resource => resource.Mapped
                ? $"'{resource.LogicalName}' (mapped to '{resource.EffectiveName}', model name '{resource.ModelName}')"
                : $"'{resource.LogicalName}' (not mapped, model name '{resource.ModelName}')");

    /// <summary>One logical index of a table with its model and effective physical names.</summary>
    private readonly record struct EffectiveIndex(
        string LogicalName,
        string ModelName,
        bool Mapped,
        string EffectiveName);

    private static DynamoTableGroup FindGroup(
        IReadOnlyList<DynamoTableGroup> groups,
        string logicalTable,
        string resource)
    {
        foreach (var group in groups)
            if (string.Equals(group.LogicalName, logicalTable, StringComparison.Ordinal))
                return group;

        var declared = groups
            .Where(static group => group.LogicalName is not null)
            .Select(static group => $"'{group.LogicalName}'")
            .Order(StringComparer.Ordinal)
            .ToArray();
        throw new InvalidOperationException(
            $"The DynamoDB runtime resource-name configuration maps the {resource} of logical table '{logicalTable}', "
            + "but the model declares no logical table with that name. "
            + (declared.Length == 0
                ? "No logical table names are declared; declare one with HasLogicalTableName."
                : $"Declared logical tables: {string.Join(", ", declared)}.")
            + " Check the spelling.");
    }

    /// <summary>Builds runtime table descriptors grouped by effective physical table name.</summary>
    private static DynamoRuntimeTableModel BuildRuntimeTableModel(IReadOnlyModel model)
    {
        Dictionary<string, DynamoTableDescriptor> tables = new(StringComparer.Ordinal);

        foreach (var tableGroup in model
            .EnumerateRootEntityTypes()
            .GroupBy(static entityType => entityType.GetTableGroupName(), StringComparer.Ordinal))
        {
            var rootEntityTypes = tableGroup.ToList();
            var sourcesByRootEntityType =
                new Dictionary<string, IReadOnlyList<DynamoIndexDescriptor>>(
                    StringComparer.Ordinal);
            var sourcesByQueryEntityType =
                new Dictionary<string, IReadOnlyList<DynamoIndexDescriptor>>(
                    StringComparer.Ordinal);

            foreach (var rootEntityType in rootEntityTypes)
            {
                sourcesByRootEntityType[rootEntityType.Name] = BuildSourceDescriptors(
                    rootEntityType,
                    rootEntityType.EnumerateSecondaryIndexesInHierarchy());

                foreach (var queryEntityType in rootEntityType.GetDerivedTypesInclusive())
                    sourcesByQueryEntityType[queryEntityType.Name] = BuildSourceDescriptors(
                        queryEntityType,
                        EnumerateSecondaryIndexesForQueryEntity(queryEntityType));
            }

            ValidateSharedTableSources(tableGroup.Key, sourcesByRootEntityType);

            tables[tableGroup.Key] = new DynamoTableDescriptor(
                tableGroup.Key,
                rootEntityTypes,
                sourcesByRootEntityType,
                sourcesByQueryEntityType);
        }

        return new DynamoRuntimeTableModel(tables);
    }

    /// <summary>Builds the ordered source descriptors visible to a single queryable entity type.</summary>
    private static IReadOnlyList<DynamoIndexDescriptor> BuildSourceDescriptors(
        IReadOnlyEntityType entityType,
        IEnumerable<IReadOnlyIndex> secondaryIndexes)
    {
        var sourceEntityType = entityType.ResolveKeyMappedEntityType();
        var partitionKeyProperty = sourceEntityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not have a configured DynamoDB partition key.");

        List<DynamoIndexDescriptor> sources =
        [
            new(
                null,
                DynamoIndexSourceKind.Table,
                null,
                partitionKeyProperty,
                sourceEntityType.GetSortKeyProperty(),
                DynamoSecondaryIndexProjectionType.All)
        ];

        Dictionary<string, DynamoIndexDescriptor> secondaryIndexesByName =
            new(StringComparer.Ordinal);

        foreach (var index in secondaryIndexes)
        {
            var secondaryIndexKind = index.GetSecondaryIndexKind();
            if (secondaryIndexKind is null)
                continue;

            var indexName = index.GetSecondaryIndexName()
                ?? throw new InvalidOperationException(
                    $"Secondary index '{index.Name ?? "<unnamed>"}' on entity type '{index.DeclaringEntityType.DisplayName()}' is missing a DynamoDB index name.");

            var descriptor = secondaryIndexKind switch
            {
                DynamoSecondaryIndexKind.Global => BuildGlobalSecondaryIndexDescriptor(
                    index,
                    indexName),
                DynamoSecondaryIndexKind.Local => BuildLocalSecondaryIndexDescriptor(
                    partitionKeyProperty,
                    index,
                    indexName),
                _ => throw new InvalidOperationException(
                    $"Secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' has an unsupported kind '{secondaryIndexKind}'.")
            };

            if (!secondaryIndexesByName.TryGetValue(indexName, out var existingDescriptor))
            {
                secondaryIndexesByName[indexName] = descriptor;
                sources.Add(descriptor);
                continue;
            }

            if (HasEquivalentSourceSignature(existingDescriptor, descriptor))
                continue;

            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' defines secondary index name '{indexName}' multiple times with conflicting metadata. "
                + "Index names must be unique per DynamoDB table and map to a single index definition.");
        }

        return sources;
    }

    /// <summary>
    ///     Enumerates secondary indexes visible to the queried entity type by combining the base
    ///     hierarchy chain with the queried type itself.
    /// </summary>
    /// <remarks>
    ///     Query-time source selection must stay result-complete for the queried entity set. A
    ///     secondary index declared only on a derived subtype may be sparse for sibling/base
    ///     entities, so exposing it to base-type queries would allow auto-selection to drop rows.
    /// </remarks>
    private static IEnumerable<IReadOnlyIndex>
        EnumerateSecondaryIndexesForQueryEntity(IReadOnlyEntityType entityType)
        => entityType
            .GetAllBaseTypes()
            .Append(entityType)
            .SelectMany(static type => type.GetDeclaredIndexes())
            .Where(static index => index.GetSecondaryIndexKind() is not null)
            .OrderBy(static index => index.GetSecondaryIndexName(), StringComparer.Ordinal)
            .ThenBy(static index => index.DeclaringEntityType.Name, StringComparer.Ordinal);

    /// <summary>Builds a runtime descriptor for a global secondary index.</summary>
    private static DynamoIndexDescriptor BuildGlobalSecondaryIndexDescriptor(
        IReadOnlyIndex index,
        string indexName)
    {
        if (index.Properties.Count is < 1 or > 2)
            throw new InvalidOperationException(
                $"Global secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' must define one or two key properties.");

        var declaringEntityDisplayName = index.DeclaringEntityType.DisplayName();
        var partitionKeyProperty = DynamoSecondaryIndexProperties.AsScalar(
            declaringEntityDisplayName,
            indexName,
            index.Properties[0],
            "global secondary index partition key");
        var sortKeyProperty = index.Properties.Count == 2
            ? DynamoSecondaryIndexProperties.AsScalar(
                declaringEntityDisplayName,
                indexName,
                index.Properties[1],
                "global secondary index sort key")
            : null;

        return new DynamoIndexDescriptor(
            indexName,
            DynamoIndexSourceKind.GlobalSecondaryIndex,
            index,
            partitionKeyProperty,
            sortKeyProperty,
            index.GetSecondaryIndexProjectionType() ?? DynamoSecondaryIndexProjectionType.All);
    }

    /// <summary>Builds a runtime descriptor for a local secondary index.</summary>
    private static DynamoIndexDescriptor BuildLocalSecondaryIndexDescriptor(
        IReadOnlyProperty partitionKeyProperty,
        IReadOnlyIndex index,
        string indexName)
    {
        if (index.Properties.Count != 1)
            throw new InvalidOperationException(
                $"Local secondary index '{indexName}' on entity type '{index.DeclaringEntityType.DisplayName()}' must define exactly one alternate sort-key property.");

        var sortKeyProperty = DynamoSecondaryIndexProperties.AsScalar(
            index.DeclaringEntityType.DisplayName(),
            indexName,
            index.Properties[0],
            "local secondary index sort key");

        return new DynamoIndexDescriptor(
            indexName,
            DynamoIndexSourceKind.LocalSecondaryIndex,
            index,
            partitionKeyProperty,
            sortKeyProperty,
            index.GetSecondaryIndexProjectionType() ?? DynamoSecondaryIndexProjectionType.All);
    }

    /// <summary>Ensures shared-table root entity types do not define conflicting secondary indexes.</summary>
    private static void ValidateSharedTableSources(
        string tableName,
        IReadOnlyDictionary<string, IReadOnlyList<DynamoIndexDescriptor>> sourcesByRootEntityType)
    {
        var entityTypeNames =
            sourcesByRootEntityType
                .Keys
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();

        for (var i = 0; i < entityTypeNames.Length; i++)
        {
            for (var j = i + 1; j < entityTypeNames.Length; j++)
            {
                var leftEntityTypeName = entityTypeNames[i];
                var rightEntityTypeName = entityTypeNames[j];

                if (HaveCompatibleSharedTableSources(
                    sourcesByRootEntityType[leftEntityTypeName],
                    sourcesByRootEntityType[rightEntityTypeName]))
                    continue;

                throw new InvalidOperationException(
                    $"Entity types '{leftEntityTypeName}' and '{rightEntityTypeName}' map to DynamoDB table '{tableName}' but expose inconsistent secondary-index metadata.");
            }
        }
    }

    /// <summary>
    ///     Determines whether two source lists expose compatible shared-table secondary-index metadata.
    /// </summary>
    /// <remarks>
    ///     Shared-table entity types may expose different secondary-index sets. Compatibility only
    ///     requires agreement for overlapping secondary indexes with the same index name.
    /// </remarks>
    private static bool HaveCompatibleSharedTableSources(
        IReadOnlyList<DynamoIndexDescriptor> left,
        IReadOnlyList<DynamoIndexDescriptor> right)
    {
        var rightByName = right
            .Where(static descriptor => descriptor.IndexName is not null)
            .ToDictionary(static descriptor => descriptor.IndexName!, StringComparer.Ordinal);

        foreach (var leftDescriptor in left.Where(static descriptor
            => descriptor.IndexName is not null))
        {
            if (!rightByName.TryGetValue(leftDescriptor.IndexName!, out var rightDescriptor))
                continue;

            if (!HasEquivalentSourceSignature(leftDescriptor, rightDescriptor))
                return false;
        }

        return true;
    }

    /// <summary>Determines whether two source descriptors are equivalent for shared-table analysis.</summary>
    private static bool
        HasEquivalentSourceSignature(DynamoIndexDescriptor left, DynamoIndexDescriptor right)
        => left.IndexName == right.IndexName
            && left.Kind == right.Kind
            && left.ProjectionType == right.ProjectionType
            && left.PartitionKeyProperty.GetAttributeName()
            == right.PartitionKeyProperty.GetAttributeName()
            && left.SortKeyProperty?.GetAttributeName() == right.SortKeyProperty?.GetAttributeName()
            && GetKeyTypeCategory(GetEffectiveProviderClrType(left.PartitionKeyProperty))
            == GetKeyTypeCategory(GetEffectiveProviderClrType(right.PartitionKeyProperty))
            && GetSortKeyTypeCategory(left.SortKeyProperty)
            == GetSortKeyTypeCategory(right.SortKeyProperty);

    /// <summary>Returns the effective provider CLR type with nullable wrappers removed.</summary>
    private static Type GetEffectiveProviderClrType(IReadOnlyProperty property)
    {
        var providerType = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
        return Nullable.GetUnderlyingType(providerType) ?? providerType;
    }

    /// <summary>Maps a CLR type to the DynamoDB key type categories used in validation.</summary>
    private static DynamoKeyTypeCategory GetKeyTypeCategory(Type clrType)
        => clrType == typeof(string) ? DynamoKeyTypeCategory.String :
            clrType == typeof(byte[]) ? DynamoKeyTypeCategory.Binary :
            IsNumericType(clrType) ? DynamoKeyTypeCategory.Number :
            DynamoKeyTypeCategory.Unsupported;

    /// <summary>Determines whether a CLR type is treated as a DynamoDB numeric key type.</summary>
    private static bool IsNumericType(Type clrType)
        => Type.GetTypeCode(clrType) is TypeCode.Byte
            or TypeCode.SByte
            or TypeCode.Int16
            or TypeCode.UInt16
            or TypeCode.Int32
            or TypeCode.UInt32
            or TypeCode.Int64
            or TypeCode.UInt64
            or TypeCode.Single
            or TypeCode.Double
            or TypeCode.Decimal;

    /// <summary>Gets the key type category for an optional sort key property.</summary>
    private static DynamoKeyTypeCategory GetSortKeyTypeCategory(IReadOnlyProperty? property)
        => property is null
            ? DynamoKeyTypeCategory.Unsupported
            : GetKeyTypeCategory(GetEffectiveProviderClrType(property));

    /// <summary>Represents supported DynamoDB key type categories for runtime consistency checks.</summary>
    private enum DynamoKeyTypeCategory
    {
        Unsupported,
        String,
        Number,
        Binary
    }
}
