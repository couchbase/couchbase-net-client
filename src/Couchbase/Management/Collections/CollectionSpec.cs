using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Management.Collections
{
    public class CollectionSpec
    {
        /// <summary>
        /// The Collection name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Scope name.
        /// </summary>
        public string ScopeName { get; }

        /// <summary>
        /// MaxExpiry is the time in seconds for the TTL for new documents in the collection. It will be infinite if not set.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public TimeSpan? MaxExpiry { get; set; }

        /// <summary>
        /// Whether history retention override is enabled on this collection. If not set will default to bucket level setting.
        /// </summary>
        public bool? History { get; init; }

        public CollectionSpec(string scopeName, string name)
        {
            ScopeName = scopeName;
            Name = name;
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
