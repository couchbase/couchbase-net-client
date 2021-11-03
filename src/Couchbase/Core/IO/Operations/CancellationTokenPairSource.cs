using System;
using System.Runtime.CompilerServices;
using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Represents a pair of cancellation tokens for a K/V operation. One token is the token which
    /// is provided by the SDK consumer to request cancellation. The other is a token which
    /// represents internal cancellation reasons, such as timeouts.
    /// </summary>
    /// <remarks>
    /// It is important to <see cref="Dispose"/> this object in order to avoid memory leaks. Such leaks
    /// may occur if one of the supplied CancellationToken instances is long-lived or if <see cref="FromTimeout"/>
    /// is used to apply a timeout.
    /// </remarks>
    internal sealed class CancellationTokenPairSource : IDisposable
    {
        // If required, stores a CancellationTokenSource which combines the internal and external tokens.
        private readonly CancellationTokenSource? _globalCts;

        // If a timeout was requested for the InternalToken, stores the CTS so we can dispose it.
        private CancellationTokenSource? _timeoutCts;

        /// <summary>
        /// Token which is provided by the SDK consumer to request cancellation.
        /// </summary>
        public CancellationToken ExternalToken { get; }

        /// <summary>
        /// Token which is created for internal cancellation reasons such as timeouts.
        /// </summary>
        public CancellationToken InternalToken { get; }

        /// <summary>
        /// Token which combines the <see cref="ExternalToken"/> and <see cref="InternalToken"/>.
        /// </summary>
        public CancellationToken GlobalToken
        {
            // If we have a global CTS then both external and internal tokens can be canceled and we should
            // return the token from the global CTS. Otherwise, return whichever token can be canceled. If neither
            // can be canceled, we can return either since they're both CancellationToken.None.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _globalCts?.Token ??
               (ExternalToken.CanBeCanceled
                   ? ExternalToken
                   : InternalToken);
        }

        /// <summary>
        /// Gets a <see cref="CancellationTokenPair"/> triggered by this source.
        /// </summary>
        // CancellationTokenPair is a simple struct with one field, so this operation is very inexpensive.
        public CancellationTokenPair TokenPair => new(this);

        /// <inheritdoc cref="CancellationToken.CanBeCanceled" />
        public bool CanBeCanceled => GlobalToken.CanBeCanceled;

        /// <inheritdoc cref="CancellationToken.IsCancellationRequested" />
        public bool IsCancellationRequested => GlobalToken.IsCancellationRequested;

        /// <summary>
        /// Indicates if the pair has been canceled by the <see cref="ExternalToken"/>.
        /// </summary>
        public bool IsExternalCancellation => ExternalToken.IsCancellationRequested;

        /// <summary>
        /// Indicates if the pair has been canceled by the <see cref="InternalToken"/>.
        /// </summary>
        public bool IsInternalCancellation => InternalToken.IsCancellationRequested;

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> which triggered cancellation, or <see cref="CancellationToken.None"/>
        /// if cancellation has not been requested.
        /// </summary>
        public CancellationToken CanceledToken =>
            IsExternalCancellation
                ? ExternalToken :
                IsInternalCancellation
                    ? InternalToken
                    : default;

        /// <summary>
        /// Constructs a CancellationTokenPairSource.
        /// </summary>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        /// <param name="internalToken">Token which combines the <see cref="ExternalToken"/> with additional cancellation reasons, such as timeouts.</param>
        /// <remarks>
        /// The global token must also be canceled any time the external token is canceled.
        /// </remarks>
        public CancellationTokenPairSource(CancellationToken externalToken, CancellationToken internalToken)
        {
            ExternalToken = externalToken;
            InternalToken = internalToken;

            if (externalToken.CanBeCanceled && internalToken.CanBeCanceled)
            {
                _globalCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, internalToken);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _globalCts?.Dispose();
            Interlocked.Exchange(ref _timeoutCts, null)?.Dispose();
        }

        /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested" />
        public void ThrowIfCancellationRequested()
        {
            // Don't use GlobalToken.ThrowIfCancellationRequested because we want the OperationCanceledException
            // to have the correct CancellationToken indicating which token requested the cancellation.
            ExternalToken.ThrowIfCancellationRequested();
            InternalToken.ThrowIfCancellationRequested();
        }

        /// <inheritdoc cref="CancellationToken.Register(Action)" />
        public CancellationTokenRegistration Register(Action callback) =>
            GlobalToken.Register(callback);

        /// <inheritdoc cref="CancellationToken.Register(Action{object}, object)" />
        public CancellationTokenRegistration Register(Action<object?> callback, object? state) =>
            GlobalToken.Register(callback, state);

#if NETCOREAPP3_1_OR_GREATER

        /// <inheritdoc cref="CancellationToken.UnsafeRegister(Action{object}, object)" />
        public CancellationTokenRegistration UnsafeRegister(Action<object?> callback, object? state) =>
            GlobalToken.UnsafeRegister(callback, state);

#endif

        /// <summary>
        /// Creates a CancellationTokenPairSource using a single external token.
        /// </summary>
        /// <param name="externalToken">The external token.</param>
        /// <returns>The CancellationTokenPairSource.</returns>
        public static CancellationTokenPairSource FromExternalToken(CancellationToken externalToken) =>
            new(externalToken, default);

        /// <summary>
        /// Creates a CancellationTokenPairSource using a single internal token.
        /// </summary>
        /// <param name="internalToken">The internal token.</param>
        /// <returns>The CancellationTokenPairSource.</returns>
        public static CancellationTokenPairSource FromInternalToken(CancellationToken internalToken) =>
            new(default, internalToken);

        /// <summary>
        /// Creates a CancellationTokenPairSource with a timeout for the <see cref="InternalToken"/>.
        /// </summary>
        /// <param name="timeout">Timeout to trigger the <see cref="InternalToken"/>.</param>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        /// <returns>The CancellationTokenPairSource.</returns>
        public static CancellationTokenPairSource FromTimeout(TimeSpan timeout,
            CancellationToken externalToken = default)
        {
            var timeoutCts = new CancellationTokenSource(timeout);

            var source = new CancellationTokenPairSource(externalToken, timeoutCts.Token);

            // Store the timeout CTS on the CancellationTokenPairSource so we do cleanup on Dispose
            source._timeoutCts = timeoutCts;

            return source;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
