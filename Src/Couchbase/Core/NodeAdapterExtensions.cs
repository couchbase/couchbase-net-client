using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Core
{
    /// <summary>
    /// Provides helper methods for workng with NodeAdapter's
    /// </summary>
    public static class NodeAdapterExtensions
    {
        /// <summary>
        /// Compares two <see cref="IList{INodeAdapter}"/> instances for equality by comparing the <see cref="INodeAdapter.Hostname"/> for each item in the list.
        /// </summary>
        /// <param name="nodes">The nodes to compare.</param>
        /// <param name="other">The other to nodes for comparison.</param>
        /// <returns></returns>
        internal static bool AreEqual(this IList<INodeAdapter> nodes, IList<INodeAdapter> other)
        {
            if (nodes == null && other == null) return true;
            if (nodes == null) return false;
            if (other == null) return false;
            if (nodes.Count != other.Count) return false;

            var ordered = nodes.OrderBy(x => x.Hostname).ToArray();
            var orderedOther = other.OrderBy(x => x.Hostname).ToArray();
            for (var i = 0; i < ordered.Count(); i++)
            {
                if (string.Compare(ordered[i].Hostname,
                    orderedOther[i].Hostname,
                    StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }
            return true;
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
