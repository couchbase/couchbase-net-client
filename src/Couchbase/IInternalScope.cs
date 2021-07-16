using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase
{
    /// <summary>
    /// Interface for any non-public methods or properties that are needed on a <see cref="IScope"/>.
    /// </summary>
    internal interface IInternalScope
    {
        /// <summary>
        /// Given a fully qualified name get the Identifier for a Collection.
        /// </summary>
        /// <param name="fullyQualifiedName">A string in the format {scopeName}.{collectionName}.</param>
        /// <returns>The identifier for a collection.</returns>
        Task<uint?> GetCidAsync(string fullyQualifiedName);
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
