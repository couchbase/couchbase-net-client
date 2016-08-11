using System.Collections.Generic;
using Couchbase.N1QL;

namespace Couchbase
{
    /// <summary>
    /// Translates the <see cref="ScanConsistency"/> enumeration to it's Couchbase Server equivalents.
    /// </summary>
    internal static class ScanConsistencyNameResolver
    {
        private static readonly Dictionary<ScanConsistency, string> ScanConsistencyResolver = new Dictionary<ScanConsistency, string>
        {
#pragma warning disable 618
            {N1QL.ScanConsistency.AtPlus, "at_plus"},
#pragma warning restore 618
            {N1QL.ScanConsistency.NotBounded, "not_bounded"},
            {N1QL.ScanConsistency.RequestPlus, "request_plus"},
#pragma warning disable 618
            {N1QL.ScanConsistency.StatementPlus, "statement_plus"}
#pragma warning restore 618
        };

        /// <summary>
        /// Resolves the specified <see cref="ScanConsistency"/> to a Couchbase Server <see cref="string"/> equivalent.
        /// </summary>
        /// <param name="scanConsistency">The scam consistency.</param>
        /// <returns></returns>
        public static string Resolve(ScanConsistency scanConsistency)
        {
            return ScanConsistencyResolver[scanConsistency];
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
