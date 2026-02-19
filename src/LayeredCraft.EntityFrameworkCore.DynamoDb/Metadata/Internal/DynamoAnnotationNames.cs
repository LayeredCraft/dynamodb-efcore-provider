namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;

public static class DynamoAnnotationNames
{
    private const string Prefix = "Dynamo:";

    public const string TableName = Prefix + "TableName";

    public const string ContainingAttributeName = Prefix + "ContainingAttributeName";

    public const string AttributeName = Prefix + "AttributeName";
}
