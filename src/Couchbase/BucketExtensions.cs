using System;
using System.Threading.Tasks;
using Couchbase.Services.Views;

namespace Couchbase
{
    public static class BucketExtensions
    {
        public static Task<IScope> Scope(this IBucket bucket, string name)
        {
            return bucket[name];
        }

        public static Task<IViewResult<T>> ViewQueryAsync<T>(this IBucket bucket, string designDocument,
            string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return bucket.ViewQueryAsync<T>(designDocument, viewName, options);
        }

        public static Task<IViewResult<T>> SpatialViewQueryAsync<T>(this IBucket bucket, string designDocument,
            string viewName, Action<SpatialViewOptions> configureOptions)
        {
            var options = new SpatialViewOptions();
            configureOptions(options);

            return bucket.SpatialViewQueryAsync<T>(designDocument, viewName, options);
        }
    }
}
