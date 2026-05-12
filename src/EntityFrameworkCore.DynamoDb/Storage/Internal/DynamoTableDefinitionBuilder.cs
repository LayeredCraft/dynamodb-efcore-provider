using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>Builds DynamoDB table lifecycle requests from provider runtime metadata.</summary>
internal static class DynamoTableDefinitionBuilder
{
    /// <summary>Builds deterministic <see cref="CreateTableRequest" /> instances for every mapped physical table.</summary>
    /// <param name="runtimeModel">Runtime DynamoDB table model.</param>
    /// <returns>Create-table requests sorted by physical table name.</returns>
    public static IReadOnlyList<CreateTableRequest>
        BuildCreateTableRequests(DynamoRuntimeTableModel runtimeModel)
        => runtimeModel
            .Tables
            .Values
            .OrderBy(static table => table.TableName, StringComparer.Ordinal)
            .Select(BuildCreateTableRequest)
            .ToArray();

    /// <summary>Builds missing GSI creations after validating an existing table description.</summary>
    /// <param name="table">Expected table descriptor.</param>
    /// <param name="existing">Existing DynamoDB table description.</param>
    /// <returns>Global secondary index updates needed for the existing table.</returns>
    public static IReadOnlyList<GlobalSecondaryIndexUpdate> BuildMissingGlobalSecondaryIndexUpdates(
        DynamoTableDescriptor table,
        TableDescription existing)
    {
        var expected = BuildCreateTableRequest(table);
        ValidateAttributeDefinitions(
            $"table '{table.TableName}'",
            expected.AttributeDefinitions,
            existing.AttributeDefinitions);
        ValidateKeySchema($"table '{table.TableName}'", expected.KeySchema, existing.KeySchema);
        ValidateLocalSecondaryIndexes(
            table.TableName,
            expected.LocalSecondaryIndexes,
            existing.LocalSecondaryIndexes);

        var existingGsies =
            (existing.GlobalSecondaryIndexes ?? []).ToDictionary(
                static index => index.IndexName,
                StringComparer.Ordinal);
        List<GlobalSecondaryIndexUpdate> updates = [];

        foreach (var expectedGsi in expected.GlobalSecondaryIndexes ?? [])
        {
            if (!existingGsies.TryGetValue(expectedGsi.IndexName, out var existingGsi))
            {
                updates.Add(
                    new GlobalSecondaryIndexUpdate
                    {
                        Create = new CreateGlobalSecondaryIndexAction
                        {
                            IndexName = expectedGsi.IndexName,
                            KeySchema = expectedGsi.KeySchema,
                            Projection = expectedGsi.Projection,
                        },
                    });
                continue;
            }

            ValidateKeySchema(
                $"global secondary index '{expectedGsi.IndexName}'",
                expectedGsi.KeySchema,
                existingGsi.KeySchema);
            ValidateProjection(
                $"global secondary index '{expectedGsi.IndexName}'",
                expectedGsi.Projection,
                existingGsi.Projection);
        }

        return updates;
    }

    private static CreateTableRequest BuildCreateTableRequest(DynamoTableDescriptor table)
    {
        var sources = DistinctSources(table);
        var baseSource =
            sources.Single(static source => source.Kind == DynamoIndexSourceKind.Table);
        Dictionary<string, ScalarAttributeType> attributeDefinitions = new(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            AddAttributeDefinition(attributeDefinitions, source.PartitionKeyProperty);
            if (source.SortKeyProperty is { } sortKeyProperty)
                AddAttributeDefinition(attributeDefinitions, sortKeyProperty);
        }

        var request = new CreateTableRequest
        {
            TableName = table.TableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions = attributeDefinitions
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new AttributeDefinition(pair.Key, pair.Value))
                .ToList(),
            KeySchema = BuildKeySchema(baseSource),
        };

        var gsis = sources
            .Where(static source => source.Kind == DynamoIndexSourceKind.GlobalSecondaryIndex)
            .OrderBy(static source => source.IndexName, StringComparer.Ordinal)
            .Select(static source => new GlobalSecondaryIndex
            {
                IndexName = source.IndexName,
                KeySchema = BuildKeySchema(source),
                Projection = BuildProjection(source),
            })
            .ToList();
        if (gsis.Count > 0)
            request.GlobalSecondaryIndexes = gsis;

        var tableHash = baseSource.PartitionKeyProperty.GetAttributeName();
        var lsis = sources
            .Where(static source => source.Kind == DynamoIndexSourceKind.LocalSecondaryIndex)
            .OrderBy(static source => source.IndexName, StringComparer.Ordinal)
            .Select(source =>
            {
                if (!string.Equals(
                    source.PartitionKeyProperty.GetAttributeName(),
                    tableHash,
                    StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Local secondary index '{source.IndexName}' on table '{table.TableName}' must use table partition key '{tableHash}'.");
                if (source.SortKeyProperty is null)
                    throw new InvalidOperationException(
                        $"Local secondary index '{source.IndexName}' on table '{table.TableName}' must define a sort key.");

                return new LocalSecondaryIndex
                {
                    IndexName = source.IndexName,
                    KeySchema = BuildKeySchema(source),
                    Projection = BuildProjection(source),
                };
            })
            .ToList();
        if (lsis.Count > 0)
            request.LocalSecondaryIndexes = lsis;

        return request;
    }

    private static IReadOnlyList<DynamoIndexDescriptor> DistinctSources(DynamoTableDescriptor table)
    {
        Dictionary<string, DynamoIndexDescriptor> sources = new(StringComparer.Ordinal);

        foreach (var descriptor in
            table.SourcesByRootEntityTypeName.Values.SelectMany(static descriptors => descriptors))
        {
            var key = descriptor.IndexName ?? string.Empty;
            if (sources.TryGetValue(key, out var existing))
            {
                if (!HaveSameSignature(existing, descriptor))
                    throw new InvalidOperationException(
                        $"Table '{table.TableName}' has conflicting duplicate source '{key}'.");
                continue;
            }

            sources.Add(key, descriptor);
        }

        return sources
            .Values
            .OrderBy(static source => source.Kind)
            .ThenBy(static source => source.IndexName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HaveSameSignature(DynamoIndexDescriptor left, DynamoIndexDescriptor right)
        => left.IndexName == right.IndexName
            && left.Kind == right.Kind
            && left.ProjectionType == right.ProjectionType
            && left.PartitionKeyProperty.GetAttributeName()
            == right.PartitionKeyProperty.GetAttributeName()
            && left.SortKeyProperty?.GetAttributeName() == right.SortKeyProperty?.GetAttributeName()
            && GetScalarAttributeType(left.PartitionKeyProperty)
            == GetScalarAttributeType(right.PartitionKeyProperty)
            && GetScalarAttributeType(left.SortKeyProperty)
            == GetScalarAttributeType(right.SortKeyProperty);

    private static List<KeySchemaElement> BuildKeySchema(DynamoIndexDescriptor source)
    {
        List<KeySchemaElement> keySchema =
        [
            new(source.PartitionKeyProperty.GetAttributeName(), KeyType.HASH)
        ];
        if (source.SortKeyProperty is { } sortKeyProperty)
            keySchema.Add(new KeySchemaElement(sortKeyProperty.GetAttributeName(), KeyType.RANGE));
        return keySchema;
    }

    private static Projection BuildProjection(DynamoIndexDescriptor source)
        => source.ProjectionType switch
        {
            DynamoSecondaryIndexProjectionType.All => new Projection
            {
                ProjectionType = ProjectionType.ALL
            },
            DynamoSecondaryIndexProjectionType.KeysOnly => new Projection
            {
                ProjectionType = ProjectionType.KEYS_ONLY
            },
            DynamoSecondaryIndexProjectionType.Include => throw new NotSupportedException(
                $"Secondary index '{source.IndexName}' uses Include projection, but non-key projected attributes are not tracked yet. Use All or KeysOnly."),
            _ => throw new InvalidOperationException(
                $"Unknown projection type '{source.ProjectionType}'."),
        };

    private static void AddAttributeDefinition(
        Dictionary<string, ScalarAttributeType> definitions,
        IReadOnlyProperty property)
    {
        var name = property.GetAttributeName();
        var type = GetScalarAttributeType(property)
            ?? throw new NotSupportedException(
                $"DynamoDB key attribute '{name}' uses unsupported CLR/provider type '{GetEffectiveProviderClrType(property).Name}'.");

        if (definitions.TryGetValue(name, out var existing) && existing != type)
            throw new InvalidOperationException(
                $"DynamoDB key attribute '{name}' maps to conflicting scalar types '{existing}' and '{type}'.");

        definitions[name] = type;
    }

    private static ScalarAttributeType? GetScalarAttributeType(IReadOnlyProperty? property)
    {
        if (property is null)
            return null;

        var clrType = GetEffectiveProviderClrType(property);
        if (clrType == typeof(string))
            return ScalarAttributeType.S;
        if (clrType == typeof(byte[]))
            return ScalarAttributeType.B;
        return IsNumericType(clrType) ? ScalarAttributeType.N : null;
    }

    private static Type GetEffectiveProviderClrType(IReadOnlyProperty property)
    {
        var providerType = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
        return Nullable.GetUnderlyingType(providerType) ?? providerType;
    }

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

    private static void ValidateAttributeDefinitions(
        string target,
        List<AttributeDefinition> expected,
        List<AttributeDefinition> actual)
    {
        var actualByName = actual.ToDictionary(
            static definition => definition.AttributeName,
            StringComparer.Ordinal);
        foreach (var expectedDefinition in expected)
        {
            if (!actualByName.TryGetValue(
                    expectedDefinition.AttributeName,
                    out var actualDefinition)
                || expectedDefinition.AttributeType != actualDefinition.AttributeType)
                throw new InvalidOperationException(
                    $"Existing {target} key attribute definition for '{expectedDefinition.AttributeName}' does not match EF model metadata.");
        }
    }

    private static void ValidateLocalSecondaryIndexes(
        string tableName,
        List<LocalSecondaryIndex>? expectedIndexes,
        List<LocalSecondaryIndexDescription>? existingIndexes)
    {
        var existingByName =
            (existingIndexes ?? []).ToDictionary(
                static index => index.IndexName,
                StringComparer.Ordinal);
        foreach (var expected in expectedIndexes ?? [])
        {
            if (!existingByName.TryGetValue(expected.IndexName, out var existing))
                throw new InvalidOperationException(
                    $"Existing table '{tableName}' is missing local secondary index '{expected.IndexName}'. Local secondary indexes cannot be added after table creation.");

            ValidateKeySchema(
                $"local secondary index '{expected.IndexName}'",
                expected.KeySchema,
                existing.KeySchema);
            ValidateProjection(
                $"local secondary index '{expected.IndexName}'",
                expected.Projection,
                existing.Projection);
        }
    }

    private static void ValidateKeySchema(
        string target,
        List<KeySchemaElement> expected,
        List<KeySchemaElement> actual)
    {
        if (expected.Count != actual.Count
            || expected
                .Zip(actual)
                .Any(static pair
                    => pair.First.AttributeName != pair.Second.AttributeName
                    || pair.First.KeyType != pair.Second.KeyType))
            throw new InvalidOperationException(
                $"Existing {target} key schema does not match EF model metadata.");
    }

    private static void ValidateProjection(string target, Projection expected, Projection actual)
    {
        if (expected.ProjectionType != actual.ProjectionType)
            throw new InvalidOperationException(
                $"Existing {target} projection does not match EF model metadata.");
    }
}
