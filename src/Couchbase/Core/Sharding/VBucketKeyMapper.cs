using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;

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
                throw new ArgumentNullException(nameof(config));
            }
            _vBucketFactory = vBucketFactory ?? throw new ArgumentNullException(nameof(vBucketFactory));

            Rev = config.Rev;
            _vBucketServerMap = vBucketServerMap ?? throw new ArgumentNullException(nameof(vBucketServerMap));
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

        public IMappedNode MapKey(string key, uint revision)
        {
            //its a retry
            if (revision > 0 && revision == Rev && HasForwardMap())
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

        public short GetIndex(string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var hash = Crc32.ComputeHash(keyBytes);

            return (short) (hash & _mask);
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<short, IVBucket> CreateVBucketMap()
        {
            var vBuckets = new Dictionary<short, IVBucket>();
            var vBucketMap = _vBucketServerMap.VBucketMap;

            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var primary = vBucketMap[i][0];
                var replicas = new short[vBucketMap[i].Length-1];
                for (var r = 1; r < vBucketMap[i].Length; r++)
                {
                    replicas[r - 1] = vBucketMap[i][r];
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
            var vBucketMapForwards = new Dictionary<short, IVBucket>();
            var vBucketMapForward = _vBucketServerMap.VBucketMapForward;

            if (vBucketMapForward != null)
            {
                for (var i = 0; i < vBucketMapForward.Length; i++)
                {
                    var primary = vBucketMapForward[i][0];
                    var replicas = new short[vBucketMapForward[i].Length-1];
                    for (var r = 1; r < vBucketMapForward[i].Length; r++)
                    {
                        replicas[r - 1] = vBucketMapForward[i][r];
                    }
                    vBucketMapForwards.Add((short)i,
                        _vBucketFactory.Create(_endPoints, (short)i, primary, replicas, Rev, _vBucketServerMap, _bucketName));
                }
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

        public uint Rev { get; set; }
    }
}
