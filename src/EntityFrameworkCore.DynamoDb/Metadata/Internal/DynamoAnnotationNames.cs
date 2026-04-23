namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Represents the DynamoAnnotationNames type.</summary>
public static class DynamoAnnotationNames
{
    private const string Prefix = "Dynamo:";

    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = Prefix + "TableName";

    /// <summary>Provides functionality for this member.</summary>
    public const string ContainingAttributeName = Prefix + "ContainingAttributeName";

    /// <summary>Provides functionality for this member.</summary>
    public const string AttributeName = Prefix + "AttributeName";

    /// <summary>Provides functionality for this member.</summary>
    public const string PartitionKeyPropertyName = Prefix + "PartitionKeyPropertyName";

    /// <summary>Provides functionality for this member.</summary>
    public const string SortKeyPropertyName = Prefix + "SortKeyPropertyName";

    /// <summary>Provides functionality for this member.</summary>
    public const string DiscriminatorStrategy = Prefix + "DiscriminatorStrategy";

    /// <summary>Marks a shared-table mapping group as explicitly opting out of discriminator metadata.</summary>
    public const string DiscriminatorDisabled = Prefix + "DiscriminatorDisabled";

    /// <summary>Provides functionality for this member.</summary>
    public const string SecondaryIndexName = Prefix + "SecondaryIndexName";

    /// <summary>Provides functionality for this member.</summary>
    public const string SecondaryIndexKind = Prefix + "SecondaryIndexKind";

    /// <summary>Provides functionality for this member.</summary>
    public const string SecondaryIndexProjectionType = Prefix + "SecondaryIndexProjectionType";

    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>
    ///     Marks the provider-created shadow int property that serves as the ordinal key for an owned
    ///     collection element (<c>OwnsMany</c>). Only properties stamped with this annotation are
    ///     treated as the ordinal; user-defined shadow int properties on the same entity type are not
    ///     affected.
    /// </summary>
    public const string OwnedOrdinalKey = Prefix + "OwnedOrdinalKey";
}
