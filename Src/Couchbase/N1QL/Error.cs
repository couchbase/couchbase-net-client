using System.Text;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents additional information returned from a N1QL query when an error has occurred.
    /// </summary>
    public sealed class Error
    {
        public string Caller { get; set; }

        public int Code { get; set; }

        public string Cause { get; set; }

        public string Key { get; set; }

        public string Message { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Caller: {0}", Caller);
            sb.AppendFormat("Code: {0}", Code);
            sb.AppendFormat("Cause: {0}", Cause);
            sb.AppendFormat("Key: {0}", Key);
            sb.AppendFormat("Message: {0}", Message);

            return sb.ToString();
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