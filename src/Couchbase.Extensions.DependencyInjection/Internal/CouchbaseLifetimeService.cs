using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class CouchbaseLifetimeService :  ICouchbaseLifetimeService
    {
        private readonly IServiceProvider _serviceProvider;

        public CouchbaseLifetimeService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async ValueTask CloseAsync()
        {
            await (_serviceProvider.GetService<IBucketProvider>()?.DisposeAsync() ?? default);
            await (_serviceProvider.GetService<IClusterProvider>()?.DisposeAsync() ?? default);
        }
    }
}
