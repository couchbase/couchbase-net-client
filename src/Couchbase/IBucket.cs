using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using PingOptions = Couchbase.Diagnostics.PingOptions;

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

        Task<IPingReport> PingAsync(PingOptions options);
    }
}
