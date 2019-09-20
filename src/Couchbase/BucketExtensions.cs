using System;
using System.Threading.Tasks;
using Couchbase.Services.KeyValue;
using Couchbase.Services.Views;

namespace Couchbase
{
    public static class BucketExtensions
    {
        public static Task<IScope> ScopeAsync(this IBucket bucket, string name)
        {
            return bucket[name];
        }

        public static Task<ICollection> DefaultCollectionAsync(this IBucket bucket)
        {
            return bucket.DefaultCollectionAsync(new CollectionOptions());
        }

        public static Task<ICollection> DefaultCollectionAsync(this IBucket bucket, Action<CollectionOptions> configureOptions)
        {
            var options = new CollectionOptions();
            configureOptions?.Invoke(options);

            return bucket.DefaultCollectionAsync(options);
        }

        public static Task<ICollection> CollectionAsync(this IBucket bucket, string scopeName, string connectionName, Action<CollectionOptions> configureOptions)
        {
            var options = new CollectionOptions();
            configureOptions?.Invoke(options);

            return bucket.CollectionAsync(scopeName, connectionName, options);
        }

        public static Task<IViewResult<T>> ViewQueryAsync<T>(this IBucket bucket, string designDocument,
            string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return bucket.ViewQueryAsync<T>(designDocument, viewName, options);
        }
    }
}
