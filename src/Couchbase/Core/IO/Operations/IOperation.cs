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

        Memory<byte> Data { get; }

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

        void Cancel();

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
        void HandleOperationCompleted(IMemoryOwner<byte> data);

        BucketConfig GetConfig(ITypeTranscoder transcoder);

        /// <summary>
        /// Task which indicates completion of the operation. Once this task is complete,
        /// the result has been received and, if successful, read.
        /// </summary>
        Task<ResponseStatus> Completed { get; }

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
