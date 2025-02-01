using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Management.Collections
{
    public class CollectionSpec : IEquatable<CollectionSpec>
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

        public bool Equals(CollectionSpec other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && ScopeName == other.ScopeName;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CollectionSpec)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, ScopeName);
        }

        public static bool operator ==(CollectionSpec left, CollectionSpec right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CollectionSpec left, CollectionSpec right)
        {
            return !Equals(left, right);
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
