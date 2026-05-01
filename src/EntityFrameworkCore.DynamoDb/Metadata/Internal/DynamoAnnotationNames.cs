namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Well-known annotation keys used by the DynamoDB EF Core provider.</summary>
public static class DynamoAnnotationNames
{
    private const string Prefix = "Dynamo:";

    /// <summary>Annotation key for the DynamoDB table name on an entity type.</summary>
    public const string TableName = Prefix + "TableName";

    /// <summary>Annotation key for the DynamoDB attribute name on a property or complex property.</summary>
    public const string AttributeName = Prefix + "AttributeName";

    /// <summary>Annotation key storing the partition key property name on an entity type.</summary>
    public const string PartitionKeyPropertyName = Prefix + "PartitionKeyPropertyName";

    /// <summary>Annotation key storing the sort key property name on an entity type.</summary>
    public const string SortKeyPropertyName = Prefix + "SortKeyPropertyName";

    /// <summary>Annotation key for the discriminator strategy applied to a shared-table entity type.</summary>
    public const string DiscriminatorStrategy = Prefix + "DiscriminatorStrategy";

    /// <summary>Marks a shared-table mapping group as explicitly opting out of discriminator metadata.</summary>
    public const string DiscriminatorDisabled = Prefix + "DiscriminatorDisabled";

    /// <summary>Annotation key for the secondary index name on an entity type or property.</summary>
    public const string SecondaryIndexName = Prefix + "SecondaryIndexName";

    /// <summary>Annotation key for the secondary index kind (GSI or LSI) on an entity type.</summary>
    public const string SecondaryIndexKind = Prefix + "SecondaryIndexKind";

    /// <summary>
    ///     Annotation key for the secondary index projection type (All, KeysOnly, Include) on an
    ///     entity type.
    /// </summary>
    public const string SecondaryIndexProjectionType = Prefix + "SecondaryIndexProjectionType";

    /// <summary>
    ///     Annotation key for the compiled runtime table model cached on an entity type after model
    ///     finalization.
    /// </summary>
    public const string RuntimeTableModel = Prefix + "RuntimeTableModel";

    /// <summary>
    ///     Marks a property as runtime-only provider metadata. Runtime-only properties are not
    ///     projected from DynamoDB item attributes and are excluded from write serialization and model
    ///     validation checks.
    /// </summary>
    public const string RuntimeOnlyProperty = Prefix + "RuntimeOnlyProperty";

    /// <summary>Identifies the runtime value source used to materialize a runtime-only property.</summary>
    public const string RuntimeValueSource = Prefix + "RuntimeValueSource";

    /// <summary>
    ///     Annotation key storing the <c>DynamoNamingConventionDescriptor</c> for an entity type. The
    ///     descriptor object is stored as the annotation value; it is not serialized to a model snapshot,
    ///     so custom delegate translators are supported freely during model building.
    /// </summary>
    public const string AttributeNamingConvention = Prefix + "AttributeNamingConvention";
}
