using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
     internal sealed class VBucketServerMap : IEquatable<VBucketServerMap>
     {
        public VBucketServerMap(VBucketServerMapDto serverMapDto, IList<IPEndPoint> ipEndPoints)
        {
            if (serverMapDto == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serverMapDto));
            }
            if (ipEndPoints == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(ipEndPoints));
            }

            HashAlgorithm = serverMapDto.HashAlgorithm;
            NumReplicas = serverMapDto.NumReplicas;
            ServerList = serverMapDto.ServerList;
            VBucketMap = serverMapDto.VBucketMap;
            VBucketMapForward = serverMapDto.VBucketMapForward;

            IPEndPoints = new ReadOnlyCollection<IPEndPoint>(ipEndPoints);
        }

        public string HashAlgorithm { get; }
        public int NumReplicas { get; }
        public string[] ServerList { get; }
        public short[][] VBucketMap { get; }
        public short[][] VBucketMapForward { get; }

        // ReSharper disable once InconsistentNaming
        public ReadOnlyCollection<IPEndPoint> IPEndPoints { get; }

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
