using System;
using Common.Logging;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    internal class OperationResult<T> : IOperationResult<T>
    {
        private readonly static ILog _log = LogManager.GetCurrentClassLogger();
        private readonly OperationBase<T> _operation;

        public OperationResult(OperationBase<T> operation)
        {
            _operation = operation;
        }

        /// <summary>
        /// Returns true if the operation completed succesfully.
        /// </summary>
        public virtual bool Success
        {
            get
            {
                var header = _operation.Header;
                return header.Status == ResponseStatus.Success &&
                    _operation.Exception == null;
            }
        }

        public T Value
        {
            get
            {
                var value = default(T);
                if (Success)
                {
                    value = _operation.GetValue();
                }
                    return value;
            }
        }

        public string Message
        {
            get { return _operation.GetMessage(); }
        }

        /// <summary>
        /// Gets the <see cref="ResponseStatus"/> of the operation that was executed.
        /// </summary>
        public virtual ResponseStatus Status
        {
            get
            {
                var status = _operation.Header.Status;
                if (_operation.Exception != null)
                {
                    status = ResponseStatus.ClientFailure;
                }
                return status;
            }
        }

        /// <summary>
        /// A numeric value for enforcing optomistic concurreny
        /// </summary>
        public virtual ulong Cas
        {
            get { return _operation.Header.Cas; }
        }

        /// <summary>
        /// Gets a <see cref="IBucketConfig"/> object if the operation was preempted by a configuration change.
        /// </summary>
        /// <returns>The latest <see cref="IBucketConfig"/> configuration.</returns>
        /// <remarks>This method is for internal use only.</remarks>
        public virtual IBucketConfig GetConfig()
        {
            IBucketConfig config = null;
            if (Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                try
                {
                    var offset = OperationBase<T>.HeaderLength + _operation.Header.ExtrasLength;
                    var length = _operation.Header.BodyLength - _operation.Header.ExtrasLength;

                    var serializer = _operation.Serializer;
                    config = serializer.Deserialize<BucketConfig>(_operation.Data.ToArray(), offset, length);
                    _log.Info(m => m("Received config rev#{0}", config.Rev));
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
            return config;
        }
    }
}

#region [ License information ]

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

#endregion [ License information ]