using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.HTTP
{
    public static class UriExtensions
    {
        /// <summary>
        /// Sets in ServicePoint properties if using a pre-.NET3.1 runtime.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to set the property on that matches the <see cref="ServicePointManager"/></param>
        /// <param name="options">The <see cref="ClusterOptions"/> with options for the <see cref="ServicePointManager"/></param>
        /// <param name="logger">The <see cref="ILogger"/> for logging any exceptions.</param>
        /// <remarks>For .NET versions 3.1 this method is ignored; set on the SocketsHttpHandler directly.</remarks>
        public static Uri SetServicePointOptions(this Uri uri, ClusterOptions options, ILogger logger)
        {
#if !NET5_0_OR_GREATER
            try
            {
                var servicePoint = ServicePointManager.FindServicePoint(uri);

                if (options.IdleHttpConnectionTimeout > TimeSpan.Zero)
                {
                    servicePoint.MaxIdleTime = (int) options.IdleHttpConnectionTimeout.TotalMilliseconds;
                }

                if (options.MaxHttpConnections > 0)
                {
                    servicePoint.ConnectionLimit = options.MaxHttpConnections;
                }

                servicePoint.SetTcpKeepAlive(options.EnableTcpKeepAlives,
                    (int)options.TcpKeepAliveTime.TotalMilliseconds,
                    (int)options.TcpKeepAliveInterval.TotalMilliseconds);
            }
            catch (Exception e)
            {
                logger.LogInformation(e, "Could not set ServicePoint properties.");
            }
#endif
            return uri;
        }
    }
}
