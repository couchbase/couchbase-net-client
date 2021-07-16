using System;
using System.Runtime.InteropServices;
using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Represents a pair of cancellation tokens for a K/V operation. One token is the token which
    /// is provided by the SDK consumer to request cancellation. The other is a token which
    /// represents internal cancellation reasons, such as timeouts.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CancellationTokenPair
    {
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
        public CancellationToken GlobalToken { get; }

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
        /// Constructs a CancellationTokenPair where both tokens are the same.
        /// </summary>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        /// <param name="internalToken">Token which combines the <see cref="ExternalToken"/> with additional cancellation reasons, such as timeouts.</param>
        /// <remarks>
        /// The global token must also be canceled any time the external token is canceled.
        /// </remarks>
        public CancellationTokenPair(CancellationToken externalToken, CancellationToken internalToken)
        {
            ExternalToken = externalToken;
            InternalToken = internalToken;

            if (externalToken.CanBeCanceled)
            {
                // Dispose of the CancellationTokenSource is not required in this case because we
                // never call CancelAfter to create a timer
                GlobalToken = internalToken.CanBeCanceled && internalToken != externalToken
                    ? CancellationTokenSource.CreateLinkedTokenSource(externalToken, internalToken).Token
                    : ExternalToken;
            }
            else
            {
                // May or may not be cancelable, but this is fine in this case
                GlobalToken = internalToken;
            }
        }

        /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested" />
        public void ThrowIfCancellationRequested() => GlobalToken.ThrowIfCancellationRequested();

        /// <inheritdoc cref="CancellationToken.Register(Action)" />
        public CancellationTokenRegistration Register(Action callback) =>
            GlobalToken.Register(callback);

        /// <inheritdoc cref="CancellationToken.Register(Action{object}, object)" />
        public CancellationTokenRegistration Register(Action<object?> callback, object? state) =>
            GlobalToken.Register(callback, state);

        /// <summary>
        /// Creates a cancellation token pair using a single external token.
        /// </summary>
        /// <param name="externalToken">The external token.</param>
        /// <returns>The CancellationTokenPair.</returns>
        public static CancellationTokenPair FromExternalToken(CancellationToken externalToken) =>
            new(externalToken, default);

        /// <summary>
        /// Creates a cancellation token pair using a single internal token.
        /// </summary>
        /// <param name="internalToken">The internal token.</param>
        /// <returns>The CancellationTokenPair.</returns>
        public static CancellationTokenPair FromInternalToken(CancellationToken internalToken) =>
            new(default, internalToken);

        public static implicit operator CancellationToken(CancellationTokenPair cancellationTokenPair) =>
            cancellationTokenPair.GlobalToken;
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
