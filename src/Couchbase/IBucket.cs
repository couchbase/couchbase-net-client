using System;
using System.Threading.Tasks;
using Couchbase.Services.Views;

namespace Couchbase
{
    public interface IBucket : IDisposable
    {
        string Name { get; }

        Task<IScope> this[string name] { get; }

        Task<ICollection> DefaultCollection { get; }

        Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default);

        Task<IViewResult<T>> SpatialViewQueryAsync<T>(string designDocument, string viewName, SpatialViewOptions options = default);
    }
}
