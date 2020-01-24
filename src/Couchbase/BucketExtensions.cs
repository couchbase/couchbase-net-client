using System;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.Views;

#nullable enable

namespace Couchbase
{
    public static class BucketExtensions
    {
        /// <summary>
        /// Execute a view query.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="bucket"><seealso cref="IBucket"/> to execute the query against.</param>
        /// <param name="designDocument">Design document name.</param>
        /// <param name="viewName">View name.</param>
        /// <returns>An <seealso cref="IViewResult{TKey,TValue}"/>.</returns>
        public static Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(this IBucket bucket, string designDocument, string viewName)
        {
            return bucket.ViewQueryAsync<TKey, TValue>(designDocument, viewName, ViewOptions.Default);
        }

        /// <summary>
        /// Execute a view query.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="bucket"><seealso cref="IBucket"/> to execute the query against.</param>
        /// <param name="designDocument">Design document name.</param>
        /// <param name="viewName">View name.</param>
        /// <param name="configureOptions">Action to configure the <seealso cref="ViewOptions"/> controlling query execution.</param>
        /// <returns>An <seealso cref="IViewResult{TKey,TValue}"/>.</returns>
        public static Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(this IBucket bucket, string designDocument,
            string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return bucket.ViewQueryAsync<TKey, TValue>(designDocument, viewName, options);
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
