using System.Threading;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Generates a linear progression of sequence numbers, overlapping if the storage is exceeded.
    /// </summary>
    public static class QuerySequenceGenerator
    {
        private static int _sequenceId;

        /// <summary>
        /// Gets the next sequence in the progression.
        /// </summary>
        /// <returns></returns>
        public static uint GetNext()
        {
            var temp = Interlocked.Increment(ref _sequenceId);
            return (uint)temp;
        }

        /// <summary>
        /// Gets the next sequence in the progression as a <see cref="string"/>.
        /// </summary>
        /// <returns></returns>
        public static string GetNextAsString()
        {
            return GetNext().ToString();
        }

        /// <summary>
        /// Resets the sequence to zero. Mainly for testing.
        /// </summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _sequenceId, 0);
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
