using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using System;
using System.IO;

namespace Couchbase.IO.Operations
{
    internal interface IOperation
    {
        OperationCode OperationCode { get; }

        ITypeTranscoder Transcoder { get; }

        string Key { get; }

        Exception Exception { get; set; }

        int BodyOffset { get; }

        ulong Cas { get; set; }

        void Read(byte[] buffer, int offset, int length);

        byte[] Write();

        MemoryStream Data { get; set; }

        byte[] Buffer { get; set; }

        int LengthReceived { get; }

        int TotalLength { get; }

        string GetMessage();

        void Reset();

        OperationHeader Header { get; set; }

        OperationBody Body { get; set; }

        [Obsolete("remove after refactoring async stuff")]
        byte[] GetBuffer();

        int Attempts { get; set; }

        int MaxRetries { get; }

        IVBucket VBucket { get; set; }

        void HandleClientError(string message, ResponseStatus responseStatus);

        IBucketConfig GetConfig();

        uint Opaque { get; }

        uint Timeout { get; set; }

        bool TimedOut();

        DateTime CreationTime { get; set; }
        Func<TimingLevel, object, IOperationTimer> Timer { get; set; }

        Task ReadAsync(byte[] buffer, int offset, int length);
        void BeginTimer(TimingLevel level);
        void EndTimer(TimingLevel level);
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