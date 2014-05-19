using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Core
{
    internal class VBucket : IVBucket
    {
        private readonly List<IServer> _cluster; 
        public VBucket(List<IServer> cluster, int index, int primary, int replica)
        {
            _cluster = cluster;
            Index = index;
            Primary = primary;
            Replica = replica;
        }

        public IServer LocatePrimary()
        {
            return _cluster[Primary];
        }

        public IServer LocateReplica()
        {
            return _cluster[Replica];
        }

        public List<IServer> Replicas
        {
            get { return _cluster.Skip(1).ToList(); }
        }

        public int Index { get; private set; }

        public int Primary { get; private set; }

        public int Replica { get; private set; }
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