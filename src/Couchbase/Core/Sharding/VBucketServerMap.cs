using System;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
     internal sealed class VBucketServerMap : IEquatable<VBucketServerMap>
     {
        public VBucketServerMap(VBucketServerMapDto serverMapDto)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serverMapDto == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serverMapDto));
            }

            HashAlgorithm = serverMapDto.HashAlgorithm;
            NumReplicas = serverMapDto.NumReplicas;
            ServerList = serverMapDto.ServerList;
            VBucketMap = serverMapDto.VBucketMap;
            VBucketMapForward = serverMapDto.VBucketMapForward;

            EndPoints = new ReadOnlyCollection<HostEndpointWithPort>(
                serverMapDto.ServerList.Select(HostEndpointWithPort.Parse).ToArray());
        }

        public string HashAlgorithm { get; }
        public int NumReplicas { get; }
        public string[] ServerList { get; }
        public short[][] VBucketMap { get; }
        public short[][] VBucketMapForward { get; }

        // ReSharper disable once InconsistentNaming
        public ReadOnlyCollection<HostEndpointWithPort> EndPoints { get; }

        public bool Equals(VBucketServerMap? other)
        {
            return (other != null
                    && ServerList.AreEqual<string>(other.ServerList)
                    && VBucketMap.AreEqual(other.VBucketMap))
                    && VBucketMapForward.AreEqual(other.VBucketMapForward);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as VBucketServerMap);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + ServerList.GetCombinedHashcode();
                hash = hash * 23 + VBucketMap.GetCombinedHashcode();
                hash = hash * 23 + NumReplicas.GetHashCode();
                hash = hash * 23 + HashAlgorithm.GetHashCode();
                return hash;
            }
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
