using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Provides a means of mapping keys to nodes within a Couchbase Server and a Couchbase Bucket.
    /// </summary>
    internal class VBucketKeyMapper : IKeyMapper
    {
        private readonly short _mask = 1023;
        private readonly IVBucketFactory _vBucketFactory;
        private readonly Dictionary<short, IVBucket> _vBuckets;
        private readonly Dictionary<short, IVBucket> _vForwardBuckets;
        private readonly VBucketServerMap _vBucketServerMap;
        private readonly ICollection<IPEndPoint> _endPoints;
        private readonly string _bucketName;

        //for log redaction
       // private Func<object, string> User = RedactableArgument.UserAction;

        public VBucketKeyMapper(BucketConfig config, VBucketServerMap vBucketServerMap, IVBucketFactory vBucketFactory)
        {
            if (config == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(config));
            }
            if (vBucketServerMap == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(vBucketServerMap));
            }
            if (vBucketFactory == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(vBucketFactory));
            }

            _vBucketFactory = vBucketFactory;

            Rev = config.Rev;
            _vBucketServerMap = vBucketServerMap;
            _endPoints = _vBucketServerMap.IPEndPoints;
            _bucketName = config.Name;
            _vBuckets = CreateVBucketMap();
            _vForwardBuckets = CreateVBucketMapForwards();
            _mask =  (short) (_vBuckets.Count - 1);
        }

        /// <summary>
        /// Gets the <see cref="IVBucket"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="IVBucket"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public IVBucket this[short index] => _vBuckets[index];

        /// <summary>
        /// Maps a given Key to it's node in a Couchbase Cluster.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key)
        {
            return _vBuckets[GetIndex(key)];
        }

        public IMappedNode MapKey(string key, bool notMyVBucket)
        {
            //its a retry
            if (notMyVBucket && HasForwardMap())
            {
                //use the fast-forward map
                var index = GetIndex(key);
                return _vForwardBuckets[index];
            }

            //use the vbucket map
            return MapKey(key);
        }

        bool HasForwardMap()
        {
            return _vForwardBuckets.Count > 0;
        }

        [SkipLocalsInit] // Avoid unnecessary cost of zero-filling keyBytes in Span scenario
        public short GetIndex(string key)
        {
#if !SPAN_SUPPORT
            var keyBytes = Encoding.UTF8.GetBytes(key);
#else
            Span<byte> keyBytes = stackalloc byte[OperationHeader.MaxKeyLength];
            var bytes = Encoding.UTF8.GetBytes(key.AsSpan(), keyBytes);
            keyBytes = keyBytes.Slice(0, bytes);
#endif

            var hash = Crc32.ComputeHash(keyBytes);

            return (short) (hash & _mask);
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<short, IVBucket> CreateVBucketMap()
        {
            var vBucketMap = _vBucketServerMap.VBucketMap;
            var vBuckets = new Dictionary<short, IVBucket>(vBucketMap.Length);

            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var currentMap = vBucketMap[i];
                var primary = currentMap[0];

                var numReplicas = currentMap.Length - 1;
                short[] replicas;
                if (numReplicas <= 0)
                {
                    replicas = Array.Empty<short>();
                }
                else
                {
                    replicas = new short[numReplicas];
                    currentMap.AsSpan(1).CopyTo(replicas);
                }

                vBuckets.Add((short)i,
                    _vBucketFactory.Create(_endPoints, (short)i, primary, replicas, Rev, _vBucketServerMap, _bucketName));
            }
            return vBuckets;
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<short, IVBucket> CreateVBucketMapForwards()
        {
            var vBucketMapForward = _vBucketServerMap.VBucketMapForward;
            if (vBucketMapForward == null)
            {
                return new Dictionary<short, IVBucket>(0);
            }

            var vBucketMapForwards = new Dictionary<short, IVBucket>(vBucketMapForward.Length);

            for (var i = 0; i < vBucketMapForward.Length; i++)
            {
                var currentForward = vBucketMapForward[i];
                var primary = currentForward[0];

                var numReplicas = vBucketMapForward.Length - 1;
                short[] replicas;
                if (numReplicas <= 0)
                {
                    replicas = Array.Empty<short>();
                }
                else
                {
                    replicas = new short[numReplicas];
                    currentForward.AsSpan(1).CopyTo(replicas);
                }

                vBucketMapForwards.Add((short)i,
                    _vBucketFactory.Create(_endPoints, (short)i, primary, replicas, Rev, _vBucketServerMap, _bucketName));
            }

            return vBucketMapForwards;
        }

        internal Dictionary<short, IVBucket> GetVBuckets()
        {
            return _vBuckets;
        }

        internal Dictionary<short, IVBucket> GetVBucketsForwards()
        {
            return _vForwardBuckets;
        }

        public ulong Rev { get; set; }
    }
}
