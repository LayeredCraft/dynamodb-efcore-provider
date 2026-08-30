using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Diagnostics;

/// <summary>Identifies an AWS SDK command issued by the provider.</summary>
public enum DynamoDbCommandOperation
{
    /// <summary>A paged PartiQL read issued through ExecuteStatement.</summary>
    ExecuteStatementQuery,

    /// <summary>A single PartiQL write issued through ExecuteStatement.</summary>
    ExecuteStatementWrite,

    /// <summary>An atomic PartiQL transaction.</summary>
    ExecuteTransaction,

    /// <summary>A non-atomic PartiQL batch write.</summary>
    BatchExecuteStatement
}

/// <summary>Contextual information about an AWS SDK command issued by the provider.</summary>
public class DynamoDbCommandEventData(
    DbContext? context,
    AmazonWebServiceRequest request,
    DynamoDbCommandOperation operation,
    Guid commandId,
    int attemptNumber,
    int? pageNumber)
{
    /// <summary>Gets current DbContext when command originates from a DbContext operation.</summary>
    public virtual DbContext? Context { get; } = context;

    /// <summary>Gets AWS SDK request submitted by the provider.</summary>
    public virtual AmazonWebServiceRequest Request { get; } = request;

    /// <summary>Gets SDK command operation.</summary>
    public virtual DynamoDbCommandOperation Operation { get; } = operation;

    /// <summary>Gets provider-generated identifier for this SDK command attempt.</summary>
    public virtual Guid CommandId { get; } = commandId;

    /// <summary>Gets one-based execution-strategy attempt number for this command.</summary>
    public virtual int AttemptNumber { get; } = attemptNumber;

    /// <summary>Gets one-based query page number, or <see langword="null" /> for writes.</summary>
    public virtual int? PageNumber { get; } = pageNumber;
}

/// <summary>Contextual information about a completed or canceled AWS SDK command.</summary>
public class DynamoDbCommandEndEventData(
    DbContext? context,
    AmazonWebServiceRequest request,
    DynamoDbCommandOperation operation,
    Guid commandId,
    int attemptNumber,
    int? pageNumber,
    TimeSpan elapsed,
    string? requestId) : DynamoDbCommandEventData(
    context,
    request,
    operation,
    commandId,
    attemptNumber,
    pageNumber)
{
    /// <summary>Gets duration of AWS SDK invocation, excluding interceptor callback time.</summary>
    public virtual TimeSpan Elapsed { get; } = elapsed;

    /// <summary>Gets AWS request identifier when available.</summary>
    public virtual string? RequestId { get; } = requestId;
}

/// <summary>Contextual information about a successful AWS SDK command.</summary>
public class DynamoDbCommandExecutedEventData(
    DbContext? context,
    AmazonWebServiceRequest request,
    DynamoDbCommandOperation operation,
    Guid commandId,
    int attemptNumber,
    int? pageNumber,
    TimeSpan elapsed,
    string? requestId,
    AmazonWebServiceResponse response,
    IReadOnlyList<ConsumedCapacity>? consumedCapacities) : DynamoDbCommandEndEventData(
    context,
    request,
    operation,
    commandId,
    attemptNumber,
    pageNumber,
    elapsed,
    requestId)
{
    /// <summary>Gets AWS SDK response returned by DynamoDB.</summary>
    public virtual AmazonWebServiceResponse Response { get; } = response;

    /// <summary>Gets capacity entries returned by DynamoDB, when requested.</summary>
    public virtual IReadOnlyList<ConsumedCapacity>? ConsumedCapacities { get; } =
        consumedCapacities;
}

/// <summary>Contextual information about a failed AWS SDK command.</summary>
public class DynamoDbCommandErrorEventData(
    DbContext? context,
    AmazonWebServiceRequest request,
    DynamoDbCommandOperation operation,
    Guid commandId,
    int attemptNumber,
    int? pageNumber,
    TimeSpan elapsed,
    string? requestId,
    Exception exception) : DynamoDbCommandEndEventData(
    context,
    request,
    operation,
    commandId,
    attemptNumber,
    pageNumber,
    elapsed,
    requestId)
{
    /// <summary>Gets exception that caused command failure.</summary>
    public virtual Exception Exception { get; } = exception;
}

/// <summary>Intercepts AWS DynamoDB SDK commands issued by this provider.</summary>
/// <remarks>
/// Callbacks observe provider-issued commands only. They do not support suppressing commands,
/// replacing responses, or configuring retries. AWS SDK request and response objects are exposed
/// for inspection; mutating them is unsupported.
/// </remarks>
public interface IDynamoDbCommandInterceptor : IInterceptor
{
    /// <summary>Called immediately before an ExecuteStatement command.</summary>
    ValueTask ExecuteStatementExecutingAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called immediately after an ExecuteStatement command completes.</summary>
    ValueTask ExecuteStatementExecutedAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandExecutedEventData eventData,
        ExecuteStatementResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called when an ExecuteStatement command is canceled.</summary>
    Task ExecuteStatementCanceledAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Called when an ExecuteStatement command fails.</summary>
    Task ExecuteStatementFailedAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Called immediately before an ExecuteTransaction command.</summary>
    ValueTask ExecuteTransactionExecutingAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called immediately after an ExecuteTransaction command completes.</summary>
    ValueTask ExecuteTransactionExecutedAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandExecutedEventData eventData,
        ExecuteTransactionResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called when an ExecuteTransaction command is canceled.</summary>
    Task ExecuteTransactionCanceledAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Called when an ExecuteTransaction command fails.</summary>
    Task ExecuteTransactionFailedAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Called immediately before a BatchExecuteStatement command.</summary>
    ValueTask BatchExecuteStatementExecutingAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called immediately after a BatchExecuteStatement command completes.</summary>
    ValueTask BatchExecuteStatementExecutedAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandExecutedEventData eventData,
        BatchExecuteStatementResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <summary>Called when a BatchExecuteStatement command is canceled.</summary>
    Task BatchExecuteStatementCanceledAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Called when a BatchExecuteStatement command fails.</summary>
    Task BatchExecuteStatementFailedAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Base class for AWS DynamoDB command interceptors.</summary>
public abstract class DynamoDbCommandInterceptor : IDynamoDbCommandInterceptor
{
    /// <inheritdoc />
    public virtual ValueTask ExecuteStatementExecutingAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual ValueTask ExecuteStatementExecutedAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandExecutedEventData eventData,
        ExecuteStatementResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual Task ExecuteStatementCanceledAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task ExecuteStatementFailedAsync(
        ExecuteStatementRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask ExecuteTransactionExecutingAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual ValueTask ExecuteTransactionExecutedAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandExecutedEventData eventData,
        ExecuteTransactionResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual Task ExecuteTransactionCanceledAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task ExecuteTransactionFailedAsync(
        ExecuteTransactionRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask BatchExecuteStatementExecutingAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual ValueTask BatchExecuteStatementExecutedAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandExecutedEventData eventData,
        BatchExecuteStatementResponse response,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public virtual Task BatchExecuteStatementCanceledAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task BatchExecuteStatementFailedAsync(
        BatchExecuteStatementRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
