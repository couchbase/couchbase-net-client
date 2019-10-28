using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;

namespace Couchbase
{
    public interface IBucket : IDisposable
    {
        string Name { get; }

        /// NOTE: This is uncommitted functionality and may change at any time.
        Task<IScope> this[string name] { get; }

        IScope DefaultScope();

        ICollection DefaultCollection();

        ICollection Collection(string collectionName);

        Task<IViewResult> ViewQueryAsync(string designDocument, string viewName, ViewOptions options);

        IViewIndexManager Views { get; }

        ICollectionManager Collections { get; }
    }
}
