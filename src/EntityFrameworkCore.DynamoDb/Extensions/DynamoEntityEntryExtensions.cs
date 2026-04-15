using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Microsoft.EntityFrameworkCore;

/// <summary>DynamoDB-specific extension methods for <see cref="EntityEntry" />.</summary>
public static class DynamoEntityEntryExtensions
{
    private const string ExecuteStatementResponsePropertyName = "__executeStatementResponse";

    /// <summary>
    ///     Returns the <see cref="ExecuteStatementResponse" /> from the DynamoDB page that produced
    ///     this entity, or <see langword="null" /> when the entity was not loaded from DynamoDB or was
    ///     loaded via a no-tracking query.
    /// </summary>
    /// <param name="entry">The entity entry for the tracked entity.</param>
    /// <returns>
    ///     The <see cref="ExecuteStatementResponse" /> for the page that produced this entity, or
    ///     <see langword="null" /> if unavailable.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         For auto-paged queries, entities from different pages return different response objects;
    ///         entities from the same page share the same object reference.
    ///     </para>
    ///     <para>
    ///         The <c>NextToken</c> on the response represents the pagination cursor after this entity's
    ///         page — for internally auto-paged queries it has already been consumed by the provider.
    ///     </para>
    ///     <para>
    ///         The <c>ConsumedCapacity</c> field is populated only when <c>ReturnConsumedCapacity</c>
    ///         was set on the underlying <c>ExecuteStatementRequest</c>. Use
    ///         <c>context.Database.GetDynamoClient()</c> to configure the request directly.
    ///     </para>
    ///     <para>
    ///         <c>ResponseMetadata.RequestId</c> is always populated and is useful for AWS support cases
    ///         and distributed tracing.
    ///     </para>
    /// </remarks>
    public static ExecuteStatementResponse? GetExecuteStatementResponse(this EntityEntry entry)
    {
        if (entry.Metadata.FindProperty(ExecuteStatementResponsePropertyName) is null)
            return null;

        return (ExecuteStatementResponse?)entry.Property(ExecuteStatementResponsePropertyName)
            .CurrentValue;
    }
}
