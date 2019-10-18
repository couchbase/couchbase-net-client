using System;
using System.Threading.Tasks;
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
    }
}
