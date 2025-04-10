using System;
using System.Net;
using System.Net.Http;
using Couchbase.Core.Exceptions;
using Couchbase.Core.RateLimiting;

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

                if (body.IndexOf("Maximum number of collections has been reached for scope",
                        StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    throw new QuotaLimitedException(QuotaLimitedReason.MaximumNumberOfCollectionsReached, ctx);
                }
            } else if (msg.StatusCode == (HttpStatusCode) 400 && body.IndexOf("num_fts_indexes",
                           StringComparison.InvariantCultureIgnoreCase) > 0)
            {
                throw new QuotaLimitedException(QuotaLimitedReason.MaximumNumberOfIndexesReached, ctx);
            }
        }

        public static void ThrowOnError(this HttpResponseMessage msg, ManagementErrorContext ctx)
        {
            if (msg.StatusCode == HttpStatusCode.BadRequest && ctx.Message.Contains("index not found"))
            {
                throw new IndexNotFoundException(ctx);
            }

            if (msg.StatusCode == HttpStatusCode.BadRequest &&
                ctx.Message.Contains("Number of vbuckets cannot be modified"))
            {
                throw new InvalidArgumentException(ctx);
            }
            throw new CouchbaseException
            {
                Context = ctx
            };
        }
    }
}
