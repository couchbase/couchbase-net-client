using System;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Views;

namespace Couchbase
{
    public static class BucketExtensions
    {
        public static Task<IScope> ScopeAsync(this IBucket bucket, string name)
        {
            return bucket[name];
        }

        public static Task<IViewResult> ViewQueryAsync(this IBucket bucket, string designDocument, string viewName)
        {
            return bucket.ViewQueryAsync(designDocument, viewName, ViewOptions.Default);
        }

        public static Task<IViewResult> ViewQueryAsync(this IBucket bucket, string designDocument,
            string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return bucket.ViewQueryAsync(designDocument, viewName, options);
        }

        #region PingAsync

        public static Task<IPingReport> PingAsync(this IBucket bucket, params ServiceType[] serviceTypes)
        {
            return PingAsync(bucket, Guid.NewGuid().ToString(), serviceTypes);
        }

        public static Task<IPingReport> PingAsync(this IBucket bucket, Action<PingOptions> configureOptions)
        {
            var options = new PingOptions();
            configureOptions(options);

            return bucket.PingAsync(options);
        }

        public static Task<IPingReport> PingAsync(this IBucket bucket, string reportId, params ServiceType[] serviceTypes)
        {
            return bucket.PingAsync(new PingOptions {ReportIdValue = reportId, ServiceTypesValue = serviceTypes});
        }

        #endregion

    }
}
