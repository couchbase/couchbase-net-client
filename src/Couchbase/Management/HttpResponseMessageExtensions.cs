using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.RateLimiting;
using Couchbase.Management.Buckets;

namespace Couchbase.Management
{
    public static class HttpResponseMessageExtensions
    {
        public static void ThrowIfRateLimitingError(this HttpResponseMessage msg, string body, ManagementErrorContext ctx)
        {
            if (msg.StatusCode == (HttpStatusCode) 429)
            {
                if (body.IndexOf("Limit(s) exceeded [num_concurrent_requests]",
                    StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    throw new RateLimitedException(RateLimitedReason.ConcurrentRequestLimitReached, ctx);
                }

                if (body.IndexOf("Limit(s) exceeded [ingress]",
                    StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    throw new RateLimitedException(RateLimitedReason.NetworkIngressRateLimitReached, ctx);
                }

                if (body.IndexOf("Limit(s) exceeded [egress]",
                    StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    throw new RateLimitedException(RateLimitedReason.NetworkEgressRateLimitReached, ctx);
                }

                //In this case multiple user limits were exceeded
                if (body.IndexOf("Limit(s) exceeded [", StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    throw new RateLimitedException(RateLimitedReason.NetworkEgressRateLimitReached, ctx);
                }
            }
        }

        public static void ThrowOnError(this HttpResponseMessage msg, ManagementErrorContext ctx)
        {
            throw new CouchbaseException
            {
                Context = ctx
            };
        }
    }
}
