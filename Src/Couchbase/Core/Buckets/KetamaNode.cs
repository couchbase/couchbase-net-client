namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// A cluster node mapped to a given Key.
    /// </summary>
    internal class KetamaNode : IMappedNode
    {
        private readonly IServer _server;

        public KetamaNode(IServer server)
        {
            _server = server;
        }

        /// <summary>
        /// Gets the primary node for a key.
        /// </summary>
        /// <returns>An object implementing the <see cref="IServer"/> interface,
        /// which is the node that a key is mapped to within a cluster.</returns>
        public IServer LocatePrimary()
        {
            return _server;
        }

        public uint Rev { get; internal set; }
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
