using System;
using Common.Logging;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Represents the result of a Couchbase or Memcached operation. 
    /// </summary>
    /// <typeparam name="T">The Type of the <see cref="Value"/> field.</typeparam>
    internal class OperationResult<T> : IOperationResult<T>
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
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

        /// <summary>
        /// Returns the value of an operation - this is the result of a Get or the value to be Inserted.
        /// </summary>
        /// <remarks>This Value will be the default value for T if the operation was not successful.</remarks>
        public virtual T Value
        {
            get
            {
                var value = default(T);
                if (Success)
                {
                    var serializer = _operation.Serializer;
                    var buffer = _operation.Body.Data;
                    var header = _operation.Header;
                    var offset = OperationBase<T>.HeaderLength + header.ExtrasLength;
                    var length = header.BodyLength - header.ExtrasLength;
                    value = serializer.Deserialize<T>(buffer, offset, length);
                }
                return value;
            }
        }
        
        /// <summary>
        /// If the operation failed, this will provide more detailed information about the reason why it failed.
        /// </summary>
        public virtual string Message
        {
            get
            {
                var message = string.Empty;
                if (!Success)
                {
                    if (Status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        message = ResponseStatus.VBucketBelongsToAnotherServer.ToString();
                    }
                    else
                    {
                        if (_operation.Exception == null)
                        {
                            try
                            {
                                var serializer = _operation.Serializer;
                                var buffer = _operation.Body.Data;
                                var header = _operation.Header;
                                var offset = OperationBase<T>.HeaderLength + header.ExtrasLength;
                                var length = header.BodyLength - header.ExtrasLength;
                                message = serializer.Deserialize<string>(buffer, offset, length);
                            }
                            catch (Exception e)
                            {
                                message = e.Message;
                            }
                        }
                        else
                        {
                            message = _operation.Exception.Message;
                        }
                    }
                }
                return message;
            }
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
                    config = serializer.Deserialize<BucketConfig>(_operation.Body.Data, offset, length);
                    _log.Info(m=>m("Received config rev#{0}", config.Rev));
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

#endregion