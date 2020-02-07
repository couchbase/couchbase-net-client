using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IKetamaKeyMapperFactory"/>.
    /// </summary>
    internal class KetamaKeyMapperFactory : IKetamaKeyMapperFactory
    {
        private readonly IIpEndPointService _ipEndPointService;

        public KetamaKeyMapperFactory(IIpEndPointService ipEndPointService)
        {
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
        }

        /// <inheritdoc />
        public async Task<KetamaKeyMapper> CreateAsync(BucketConfig bucketConfig, CancellationToken cancellationToken = default)
        {
            var ipEndPoints = await GetIpEndPointsAsync(bucketConfig, cancellationToken);

            return new KetamaKeyMapper(ipEndPoints);
        }

        private async Task<IList<IPEndPoint>> GetIpEndPointsAsync(BucketConfig config, CancellationToken cancellationToken)
        {
            var ipEndPoints = new List<IPEndPoint>();
            foreach (var node in config.GetNodes().Where(p => p.IsKvNode))
            {
                var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(node, cancellationToken);
                if (ipEndPoint == null)
                {
                    throw new InvalidOperationException("IP endpoint lookup failed.");
                }

                ipEndPoints.Add(ipEndPoint);
            }

            return ipEndPoints;
        }
    }
}
