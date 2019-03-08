    using System;
using System.Threading.Tasks;
    using Couchbase.Services.Views;

namespace Couchbase
{
    public interface IBucket : IDisposable
    {
        string Name { get; }

        Task BootstrapAsync(Uri uri, IConfiguration configuration);

        Task<IScope> this[string name] { get; }

        Task<ICollection> DefaultCollection { get; }

        Task<IScope> Scope(string name);

        Task<IViewResult> ViewQuery<T>(string statement, IViewOptions options);

        Task<ISpatialViewResult> SpatialViewQuery<T>(string statement, ISpatialViewOptions options);
    }
}
