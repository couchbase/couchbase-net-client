using System;
using System.Text;
using Couchbase.Core;
using Newtonsoft.Json;

namespace Couchbase
{
    /// <summary>
    /// Base exception for all exceptions generated or handled by the Couchbase SDK.
    /// </summary>
    public class CouchbaseException : Exception
    {
        public CouchbaseException() { }

        public CouchbaseException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }

        public CouchbaseException(string message) : base(message) {}

        public CouchbaseException(string message, Exception innerException) : base(message, innerException) {}

        public IErrorContext Context { get; set; }

        internal bool IsReadOnly { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(base.ToString());
            sb.AppendLine("-----------------------Context Info---------------------------");
            sb.AppendLine(JsonConvert.SerializeObject(Context));
            return sb.ToString();
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
