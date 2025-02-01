using System;
using System.Collections.Generic;

namespace Couchbase.Management.Collections
{
    public class ScopeSpec : IEquatable<ScopeSpec>
    {
        public string Name { get; }
        public IEnumerable<CollectionSpec> Collections { get; set; }

        public ScopeSpec(string name)
        {
            Name = name;
        }

        public bool Equals(ScopeSpec other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ScopeSpec)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name);
        }

        public static bool operator ==(ScopeSpec left, ScopeSpec right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ScopeSpec left, ScopeSpec right)
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
