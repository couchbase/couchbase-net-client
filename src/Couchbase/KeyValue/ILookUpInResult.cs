using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Result of a sub document LookupIn operation.
    /// </summary>
    public interface ILookupInResult : IResult
    {
        bool Exists(int index);

        bool IsDeleted { get; }

        T ContentAs<T>(int index);

        /// <summary>
        /// Returns the index of a particular path.
        /// </summary>
        /// <param name="path">Path to find.</param>
        /// <returns>The index of the path, or -1 if not found.</returns>
        [InterfaceStability(Level.Volatile)]
        int IndexOf(string path);
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
