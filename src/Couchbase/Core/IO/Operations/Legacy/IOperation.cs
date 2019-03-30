using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Core.IO.Operations.Legacy
{
    public interface IOperation : IDisposable
    {
        OpCode OpCode { get; }

        string Key { get; }

        uint Opaque { get; }

        ulong Cas { get; set; }

        uint? Cid { get; set; }

        short? VBucketId { get; set; }

        bool RequiresKey { get; }

        Exception Exception { get; set; }

        Memory<byte> Data { get; }

        uint LastConfigRevisionTried { get; set; }

        string BucketName { get; set; }

        int TotalLength { get; }

        int Attempts { get; set; }

        int MaxRetries { get; }

        IPEndPoint CurrentHost { get; set; }

        OperationHeader Header { get; set; }

        string GetMessage();

        void Reset();

        DateTime CreationTime { get; set; }

        byte[] Write();

        Task<byte[]> WriteAsync();

        Task ReadAsync(IMemoryOwner<byte> buffer, ErrorMap errorMap = null);

        void HandleClientError(string message, ResponseStatus responseStatus);

        BucketConfig GetConfig(ITypeTranscoder transcoder);

        Func<SocketAsyncState, Task> Completed { get; set; }

        bool CanRetry();

        IOperationResult GetResult();

        IOperation Clone();

        int GetRetryTimeout(int defaultTimeout);

        /// <summary>
        /// Extracts the data for this instance, if any. The data is removed, and ownership is transferred to the receiver.
        /// The receiver is now responsible for releasing the memory.
        /// </summary>
        /// <returns>The <see cref="IMemoryOwner{T}"/>.</returns>
        IMemoryOwner<byte> ExtractData();
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
