using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

internal sealed class DynamoDbCommandInterceptorAggregator
    : InterceptorAggregator<IDynamoDbCommandInterceptor>
{
    protected override IDynamoDbCommandInterceptor CreateChain(
        IEnumerable<IDynamoDbCommandInterceptor> interceptors)
        => new CompositeDynamoDbCommandInterceptor(interceptors);

    private sealed class CompositeDynamoDbCommandInterceptor(
        IEnumerable<IDynamoDbCommandInterceptor> interceptors) : IDynamoDbCommandInterceptor
    {
        private readonly IDynamoDbCommandInterceptor[] _interceptors = interceptors.ToArray();

        public async ValueTask ExecuteStatementExecutingAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteStatementExecutingAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async ValueTask ExecuteStatementExecutedAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandExecutedEventData eventData,
            ExecuteStatementResponse response,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteStatementExecutedAsync(request, eventData, response, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task ExecuteStatementCanceledAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteStatementCanceledAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task ExecuteStatementFailedAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteStatementFailedAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async ValueTask ExecuteTransactionExecutingAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteTransactionExecutingAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async ValueTask ExecuteTransactionExecutedAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandExecutedEventData eventData,
            ExecuteTransactionResponse response,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteTransactionExecutedAsync(
                        request,
                        eventData,
                        response,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task ExecuteTransactionCanceledAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteTransactionCanceledAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task ExecuteTransactionFailedAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .ExecuteTransactionFailedAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async ValueTask BatchExecuteStatementExecutingAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .BatchExecuteStatementExecutingAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async ValueTask BatchExecuteStatementExecutedAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandExecutedEventData eventData,
            BatchExecuteStatementResponse response,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .BatchExecuteStatementExecutedAsync(
                        request,
                        eventData,
                        response,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task BatchExecuteStatementCanceledAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .BatchExecuteStatementCanceledAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }

        public async Task BatchExecuteStatementFailedAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _interceptors.Length; i++)
                await _interceptors[i]
                    .BatchExecuteStatementFailedAsync(request, eventData, cancellationToken)
                    .ConfigureAwait(false);
        }
    }
}
