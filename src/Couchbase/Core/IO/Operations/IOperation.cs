using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    internal interface IOperation : IDisposable, IRequest
    {
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// If true and the server returns KeyNotFound the DocumentNotFoundException will
        /// not be thrown; instead the ResponseStatus will be returned for an Exists check.
        /// </summary>
        bool PreferReturns { get; }

        bool CanStream { get; }

        bool PreserveTtl { get; }

        /// <summary>
        /// OpCode of the operation.
        /// </summary>
        OpCode OpCode { get; }

        /// <summary>
        /// Bucket name, if applicable.
        /// </summary>
        string? BucketName { get; }

        /// <summary>
        /// Scope name, if applicable.
        /// </summary>
        string? SName { get; }

        /// <summary>
        /// Collection name, if applicable.
        /// </summary>
        string? CName { get; }

        /// <summary>
        /// Collection identifier, if applicable.
        /// </summary>
        uint? Cid { get; set; }

        /// <summary>
        /// Document key, if applicable, or an empty string.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// True if the operation requires a VBucketId, otherwise false.
        /// </summary>
        bool RequiresVBucketId { get; }

        /// <summary>
        /// vBucket identifier, if applicable.
        /// </summary>
        short? VBucketId { get; set; }

        /// <summary>
        /// Replica index for replica reads, null for all other operations.
        /// </summary>
        short? ReplicaIdx { get; }

        /// <summary>
        /// Opaque operation identifier, unique for each operation.
        /// </summary>
        uint Opaque { get; }

        /// <summary>
        /// Compare-and-swap value.
        /// </summary>
        ulong Cas { get; }

        /// <summary>
        /// Response operation header.
        /// </summary>
        OperationHeader Header { get; }

        /// <summary>
        /// Tracing span.
        /// </summary>
        IRequestSpan Span { get; }

        /// <summary>
        /// Indicates that a mutation operation has a durability requirement.
        /// </summary>
        bool HasDurability { get; }

        /// <summary>
        /// Indicates if this operation is only performing a read operation and not changing state.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Indicates if this operation has been sent down the wire to the server.
        /// </summary>
        bool IsSent { get; }

        /// <summary>
        /// Cancellation token pair which can cancel the operation. Usually set by the <see cref="OperationCancellationRegistration"/>.
        /// </summary>
        CancellationTokenPair TokenPair { get; set; }

        /// <summary>
        /// Task which indicates completion of the operation. Once this task is complete,
        /// the result has been received and, if successful, read.
        /// </summary>
        /// <remarks>
        /// It is important that rules about ValueTask be followed here. The task should only
        /// be awaited once, never more than once. Calling <see cref="Reset"/> will reset the
        /// task, after which it may be awaited again.
        ///
        /// For more information, see https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/#valid-consumption-patterns-for-valuetasks
        /// </remarks>
        ValueTask<ResponseStatus> Completed { get; }

        /// <summary>
        /// Reset the operation so it may be retried.
        /// </summary>
        void Reset();

        /// <summary>
        /// Serializes the operation body and sends it to a connection.
        /// </summary>
        /// <param name="connection">Connection on which to send the operation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task which is completed when the operation is sent.</returns>
        Task SendAsync(IConnection connection, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the operation as canceled with an optional reference to the cancellation token, if it isn't already completed.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token which triggered the cancellation.</param>
        /// <returns>True if the operation was canceled.</returns>
        bool TrySetCanceled(CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the operation as completed with the given exception, if it isn't already completed.
        /// </summary>
        /// <param name="ex">Exception which occurred.</param>
        /// <returns>True if the operation was completed with the provided exception.</returns>
        bool TrySetException(Exception ex);

        /// <summary>
        /// Called by the connection when a complete response packet is received.
        /// </summary>
        /// <param name="data">Data which was received.</param>
        /// <remarks>
        /// Ownership of the data buffer is passed to the caller, which is then responsible
        /// for disposing of the buffer. Failure to dispose may call memory leaks.
        /// </remarks>
        void HandleOperationCompleted(in SlicedMemoryOwner<byte> data);

        /// <summary>
        /// Returns a block of memory containing the body of the operation response. May only be called once.
        /// Ownership of the block of memory is transferred to the caller, which is then responsible for disposing it.
        /// </summary>
        /// <returns>An owned block of memory containing the body of the operation response.</returns>
        SlicedMemoryOwner<byte> ExtractBody();

        /// <summary>
        /// Reads <see cref="BucketConfig"/> from the response body.
        /// </summary>
        /// <param name="transcoder">Transcoder to use while reading.</param>
        /// <returns>The bucket config if the response body contains a bucket config, otherwise null.</returns>
        /// <remarks>
        /// This method generally relates to <see cref="ResponseStatus.VBucketBelongsToAnotherServer"/> responses.
        /// </remarks>
        BucketConfig? ReadConfig(ITypeTranscoder transcoder);

        /// <summary>
        /// Returns true if the previous attempt of the operation resulted in a Not My VBucket response from the server.
        /// </summary>
        /// <returns></returns>
        bool WasNmvb();

        long? LastServerDuration { get; }

        string? LastDispatchedFrom { get; }

        string? LastDispatchedTo { get; }

        bool IsCompleted { get; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information          ]
