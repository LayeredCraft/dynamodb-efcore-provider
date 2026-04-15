using Humanizer;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Translates CLR property names to DynamoDB attribute names using Humanizer.</summary>
internal static class DynamoNamingTranslator
{
    /// <summary>
    ///     Transforms <paramref name="name" /> according to the specified
    ///     <paramref name="convention" />.
    /// </summary>
    /// <param name="name">The CLR property name to transform.</param>
    /// <param name="convention">The naming convention to apply.</param>
    /// <returns>The transformed DynamoDB attribute name.</returns>
    public static string Translate(string name, DynamoAttributeNamingConvention convention)
        => convention switch
        {
            DynamoAttributeNamingConvention.None => name,
            DynamoAttributeNamingConvention.SnakeCase => name.Underscore(),
            DynamoAttributeNamingConvention.CamelCase => name.Camelize(),
            DynamoAttributeNamingConvention.KebabCase => name.Kebaberize(),
            DynamoAttributeNamingConvention.UpperSnakeCase => name.Underscore().ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(convention), convention, null),
        };
}
