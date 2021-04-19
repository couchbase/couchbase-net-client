using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Sharding
{
     /// <summary>
    /// Represents a VBucket partition in a Couchbase cluster
    /// </summary>
    internal class VBucket : IVBucket
    {
        private readonly short[] _replicas;
        private readonly VBucketServerMap _vBucketServerMap;
        private readonly ILogger<VBucket> _logger;
        private readonly ICollection<IPEndPoint> _endPoints;

        public VBucket(ICollection<IPEndPoint> endPoints, short index, short primary, short[] replicas, ulong rev,
            VBucketServerMap vBucketServerMap, string bucketName, ILogger<VBucket> logger)
        {
            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }

            _endPoints = endPoints;
            Index = index;
            Primary = primary;
            _replicas = replicas;
            Rev = rev;
            _vBucketServerMap = vBucketServerMap;
            BucketName = bucketName;
            _logger = logger;
        }

        /// <summary>
        /// Gets a reference to the primary server for this VBucket.
        /// </summary>
        /// <returns>A <see cref="IServer"/> reference which is the primary server for this <see cref="VBucket"/></returns>
        ///<remarks>If the VBucket doesn't have a active, it will return a random <see cref="IServer"/> to force a NMV and reconfig.</remarks>
        public IPEndPoint LocatePrimary()
        {
            IPEndPoint? endPoint = null;
            if (Primary > -1 && Primary < _endPoints.Count &&
                Primary < _vBucketServerMap.IPEndPoints.Count)
            {
                try
                {
                    endPoint = _vBucketServerMap.IPEndPoints[Primary];
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Error locating Primary");
                }
            }
            if(endPoint == null)
            {
                if (_replicas.Any(x => x != -1))
                {
                    var index = _replicas.GetRandom();
                    if (index > -1 && index < _endPoints.Count
                        && index < _vBucketServerMap.IPEndPoints.Count)
                    {
                        try
                        {
                            endPoint = _vBucketServerMap.IPEndPoints[index];
                        }
                        catch (Exception e)
                        {
                            _logger.LogDebug(e, "Error locating Primary");
                        }
                    }
                }
            }
            return endPoint ?? (_endPoints.GetRandom());
        }

        /// <summary>
        /// Locates a replica for a given index.
        /// </summary>
        /// <param name="index">The index of the replica.</param>
        /// <returns>An <see cref="IServer"/> if the replica is found, otherwise null.</returns>
        public IPEndPoint? LocateReplica(short index)
        {
            try
            {
                return _vBucketServerMap.IPEndPoints[index];
            }
            catch
            {
                _logger.LogDebug("No server found for replica with index of {0}.", index);
                return null;
            }
        }

        /// <summary>
        /// Gets an array of replica indexes.
        /// </summary>
        public short[] Replicas => _replicas;

        /// <summary>
        /// Gets the index of the VBucket.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        public short Index { get; }

        /// <summary>
        /// Gets the index of the primary node in the VBucket.
        /// </summary>
        /// <value>
        /// The primary index that the key has mapped to.
        /// </value>
        public short Primary { get; }

        /// <summary>
        /// Gets or sets the configuration revision.
        /// </summary>
        /// <value>
        /// The rev.
        /// </value>
        public ulong Rev { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this instance has replicas.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has replicas; otherwise, <c>false</c>.
        /// </value>
        public bool HasReplicas
        {
            get { return _replicas.Any(x => x > -1); }
        }

        public string BucketName { get; }
    }
}
