using Couchbase.IO;

namespace Couchbase
{
    /// <summary>
    /// The return type for "document" centric operation requests.
    /// </summary>
    /// <typeparam name="T">The type the value of the document will be.</typeparam>
    public interface IDocumentResult<T> : IResult
    {
        /// <summary>
        /// The Document object
        /// </summary>
        Document<T> Document { get; }

        /// <summary>
        /// The response status returned by the server when fulfilling the request.
        /// </summary>
        ResponseStatus Status { get; }

        /// <summary>
        /// The actual value stored within Couchbase
        /// </summary>
        T Content { get; }
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

#endregion
