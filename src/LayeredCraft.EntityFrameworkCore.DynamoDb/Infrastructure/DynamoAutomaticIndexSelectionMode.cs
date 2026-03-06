namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Controls how the provider should apply automatic secondary index selection.</summary>
public enum DynamoAutomaticIndexSelectionMode
{
    /// <summary>Disables automatic index selection and uses only explicit hints.</summary>
    Off,

    /// <summary>Analyzes candidate indexes and emits diagnostics without changing the query source.</summary>
    SuggestOnly,

    /// <summary>Allows conservative automatic index selection when the query shape is unambiguous.</summary>
    Conservative,
}
