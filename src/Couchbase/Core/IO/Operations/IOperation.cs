using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations
{
    internal interface IOperation : IDisposable, IRequest
    {
        OpCode OpCode { get; }

        string Key { get; }

        uint Opaque { get; }

        ulong Cas { get; set; }

        short ReplicaIdx { get; set; }

        uint? Cid { get; set; }

        short? VBucketId { get; set; }

        bool RequiresKey { get; }

        Exception Exception { get; set; }

        string CName { get; set; }

        string SName { get; set; }

        ReadOnlyMemory<byte> Data { get; }

        uint LastConfigRevisionTried { get; set; }

        string BucketName { get; set; }

        bool IsReplicaRead { get; }

        int TotalLength { get; }

        IPEndPoint CurrentHost { get; set; }

        OperationHeader Header { get; set; }

        IInternalSpan Span { get; }

        string GetMessage();

        void Reset();

        DateTime CreationTime { get; set; }

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

        void HandleClientError(string message, ResponseStatus responseStatus);

        /// <summary>
        /// Called by the connection when a complete response packet is received.
        /// </summary>
        /// <param name="data">Data which was received.</param>
        /// <remarks>
        /// Ownership of the data buffer is passed to the caller, which is then responsible
        /// for disposing of the buffer. Failure to dispose may call memory leaks.
        /// </remarks>
        void HandleOperationCompleted(in SlicedMemoryOwner<byte> data);

        BucketConfig GetConfig(ITypeTranscoder transcoder);

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
        /// Indicates if this operation is only performing a read operation and not changing state.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Indicates if this operation has been sent down the wire to the server.
        /// </summary>
        bool IsSent { get; }

        bool CanRetry();

        IOperationResult GetResult();

        IOperation Clone();

        bool HasDurability { get; }
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
