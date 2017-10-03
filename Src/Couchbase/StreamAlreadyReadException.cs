using System;

namespace Couchbase
{
    /// <summary>
    /// Thrown when an attempt is made to access a property or methods before reading the request stream via iteration.
    /// </summary>
    /// <seealso cref="InvalidOperationException" />
    public class StreamAlreadyReadException : InvalidOperationException
    {
        private const string DefaultMessage = "The underly stream has already been read and cannt be read again.";

        public StreamAlreadyReadException(string message = DefaultMessage, Exception exception = null)
            : base(message, exception)
        { }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
