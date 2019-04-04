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

        Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default);
        Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, Action<ViewOptions> configureOptions);

        Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, SpatialViewOptions options = default);
        Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, Action<SpatialViewOptions> configureOptions);
    }
}
