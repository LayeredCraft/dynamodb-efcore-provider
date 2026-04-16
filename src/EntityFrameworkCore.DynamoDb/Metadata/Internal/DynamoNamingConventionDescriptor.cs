namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>
///     Holds the naming convention configuration for an entity type — either a named
///     <see cref="DynamoAttributeNamingConvention" /> strategy or a custom
///     <see cref="Func{T, TResult}" /> delegate.
/// </summary>
/// <remarks>
///     Stored as a runtime annotation on the entity type so delegates (which cannot be serialized
///     to a model snapshot) are supported without affecting design-time tooling.
/// </remarks>
internal sealed class DynamoNamingConventionDescriptor
{
    private readonly DynamoAttributeNamingConvention? _kind;
    private readonly Func<string, string>? _translator;

    private DynamoNamingConventionDescriptor(
        DynamoAttributeNamingConvention? kind,
        Func<string, string>? translator)
    {
        _kind = kind;
        _translator = translator;
    }

    /// <summary>Creates a descriptor backed by a built-in naming convention.</summary>
    /// <param name="kind">The naming convention to use.</param>
    public static DynamoNamingConventionDescriptor Named(DynamoAttributeNamingConvention kind)
        => new(kind, null);

    /// <summary>Creates a descriptor backed by a custom translation delegate.</summary>
    /// <param name="translator">
    ///     A function that receives a CLR property name and returns the desired
    ///     DynamoDB attribute name.
    /// </param>
    public static DynamoNamingConventionDescriptor Custom(Func<string, string> translator)
        => new(null, translator);

    /// <summary>
    ///     Applies the configured strategy to <paramref name="propertyName" />, returning the
    ///     DynamoDB attribute name.
    /// </summary>
    /// <param name="propertyName">The CLR property name to transform.</param>
    /// <returns>The transformed DynamoDB attribute name.</returns>
    public string Translate(string propertyName)
        => _translator is not null
            ? _translator(propertyName)
            : DynamoNamingTranslator.Translate(propertyName, _kind!.Value);
}
