using System;
using System.Buffers;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Services.Views;

namespace Couchbase
{
    public interface IBucket : IDisposable
    {
        string Name { get; }

        Task<IScope> this[string name] { get; }

        Task<ICollection> DefaultCollection { get; }

        Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default);
    }
}
