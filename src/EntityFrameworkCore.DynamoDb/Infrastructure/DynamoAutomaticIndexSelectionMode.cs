namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Controls how the provider should apply automatic secondary index selection.</summary>
public enum DynamoAutomaticIndexSelectionMode
{
    /// <summary>Disables automatic index selection and uses only explicit hints.</summary>
    Off = 0,

    /// <summary>Analyzes candidate indexes and emits diagnostics without changing the query source.</summary>
    SuggestOnly = 1,

    /// <summary>Automatically selects an unambiguous compatible secondary index.</summary>
    On = 2,
}
