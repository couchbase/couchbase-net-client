using System;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// The parent timeout exception of <see cref="AmbiguousTimeoutException"/> and <see cref="UnambiguousTimeoutException"/>.
    /// </summary>
    public class TimeoutException : CouchbaseException
    {
        public TimeoutException() { }

        public TimeoutException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }

        public TimeoutException(string message) : base(message) { }

        public TimeoutException(string message, Exception innerException) : base(message, innerException) { }
        public TimeoutException(string message, IErrorContext errorContext) : base(errorContext, message) { }
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
