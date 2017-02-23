using System;
using Couchbase.Configuration.Client;
using Couchbase.Utils;

namespace Couchbase.IO
{
    /// <summary>
    /// Thrown if an operation does not complete before the <see cref="PoolConfiguration.SendTimeout"/> is exceeded.
    /// </summary>
    /// <seealso cref="System.TimeoutException" />
    public sealed class SendTimeoutExpiredException : TimeoutException
    {
        private string _stackTrace;

        public SendTimeoutExpiredException()
            : this(ExceptionUtil.GetMessage(ExceptionUtil.OperationTimeout))
        {
        }

        public SendTimeoutExpiredException(string message)
            : base(message)
        {
            _stackTrace = Environment.StackTrace;
            Source = CurrentAssembly.Current.GetName().Name;
        }

        public SendTimeoutExpiredException(string message, Exception innerException)
            : base(message, innerException)
        {
            _stackTrace = Environment.StackTrace;
            Source = CurrentAssembly.Current.GetName().Name;
        }

        /// <summary>
        /// Gets a string representation of the immediate frames on the call stack.
        /// </summary>
        public override string StackTrace
        {
            get { return _stackTrace; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
