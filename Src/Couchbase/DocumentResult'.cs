using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase
{
    /// <summary>
    /// The return type for "document" centric operation requests.
    /// </summary>
    /// <typeparam name="T">The type the value of the document will be.</typeparam>
    public class DocumentResult<T> : IDocumentResult<T>
    {
        public DocumentResult(IOperationResult<T> result, string id)
        {
            Document = new Document<T>
            {
                Cas = result.Cas,
                Id = id,
                Content = result.Value
            };
            Content = Document.Content;
            Message = result.Message;
            Status = result.Status;
            Success = result.Success;
            Exception = result.Exception;
        }

        /// <summary>
        /// Returns true if the operation was succesful
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// If the Success is false, a message indicating the reason why
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// The Document object
        /// </summary>
        public Document<T> Document { get; internal set; }

        /// <summary>
        /// The response status returned by the server when fulfilling the request.
        /// </summary>
        public ResponseStatus Status { get; internal set; }

        /// <summary>
        /// The actual value stored within Couchbase
        /// </summary>
        public T Content { get; set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public System.Exception Exception { get; set; }


        public bool ShouldRetry()
        {
            throw new NotImplementedException();
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

#endregion
