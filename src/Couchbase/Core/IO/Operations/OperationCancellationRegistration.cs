using System;
using System.Threading;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Upon construction, registers with a <see cref="CancellationTokenPair"/> to set the operation to canceled
    /// while following logic rules regarding external vs internal cancellation. Disposing will release the
    /// registration and prevent future cancellation of the operation.
    /// </summary>
    internal class OperationCancellationRegistration : IDisposable
    {
        private static readonly Action<object?> HandleExternalCancellationAction = HandleExternalCancellation;
        private static readonly Action<object?> HandleInternalCancellationAction = HandleInternalCancellation;

        private readonly IOperation _operation;
        private readonly CancellationTokenPair _tokenPair;

        private CancellationTokenRegistration _internalRegistration;
        private CancellationTokenRegistration _externalRegistration;

        public OperationCancellationRegistration(IOperation operation, CancellationTokenPair tokenPair)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (operation == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(operation));
            }

            _operation = operation;
            _tokenPair = tokenPair;

            // Since we're using static actions, register calls below are a fast noop for tokens which cannot be canceled
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_1
            _externalRegistration = tokenPair.ExternalToken.Register(HandleExternalCancellationAction, this);
            _internalRegistration = tokenPair.InternalToken.Register(HandleInternalCancellationAction, this);
#else
            // On .NET Core 3 and later we can further optimize by not flowing the ExecutionContext using UnsafeRegister
            _externalRegistration = tokenPair.ExternalToken.UnsafeRegister(HandleExternalCancellationAction, this);
            _internalRegistration = tokenPair.InternalToken.UnsafeRegister(HandleInternalCancellationAction, this);
#endif
        }

        public void Dispose()
        {
            // CancellationTokenRegistration is a value type and Dispose is a noop for the default value,
            // so this is safe in the case where there is no registration.
            _internalRegistration.Dispose();
            _externalRegistration.Dispose();
        }

        /// <summary>
        /// Static method for processing external cancellation. By using a static Action instance and passing the
        /// registration as state we reduce heap allocations.
        /// </summary>
        private static void HandleExternalCancellation(object? state)
        {
            var registration = (OperationCancellationRegistration) state!;
            var operation = registration._operation;
            var tokenPair = registration._tokenPair;

            // We should only cancel if the operation is:
            // 1. Currently in flight
            // 2. Is not a mutation operation
            // In those cases, we keep waiting for the response to avoid ambiguity on mutations

            if (!operation.IsSent || operation.IsReadOnly)
            {
                operation.TrySetCanceled(tokenPair.ExternalToken);
            }
        }

        /// <summary>
        /// Static method for processing internal cancellation. By using a static Action instance and passing the
        /// registration as state we reduce heap allocations.
        /// </summary>
        private static void HandleInternalCancellation(object? state)
        {
            var registration = (OperationCancellationRegistration) state!;
            var operation = registration._operation;
            var tokenPair = registration._tokenPair;

            operation.TrySetCanceled(tokenPair.InternalToken);
        }
    }
}
