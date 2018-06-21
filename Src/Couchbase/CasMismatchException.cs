using System;
using System.Runtime.Serialization;

namespace Couchbase
{
    /// <summary>
    /// Thrown when a CAS operation fails because the document has been modified and that CAS value has changed.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class CasMismatchException : Exception
    {
        public CasMismatchException()
        {
        }

        public CasMismatchException(string message) : base(message)
        {
        }

        public CasMismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if NET452
        protected CasMismatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
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
