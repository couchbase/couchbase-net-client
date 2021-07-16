namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// Raised when a comparison between a stored document's CAS does not match the CAS provided by the
    /// request indicating the document has been mutated. Each time the document changes its CAS changes.
    /// A form of optimistic concurrency.
    /// </summary>
    public class CasMismatchException : CouchbaseException
    {
        public CasMismatchException()
        {
        }

        public CasMismatchException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }
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
