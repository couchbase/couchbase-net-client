using System;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Implementation of <see cref="IServiceFactory"/> which always returns the same singleton.
    /// </summary>
    internal class SingletonServiceFactory : IServiceFactory
    {
        private readonly object _singleton;

        /// <summary>
        /// Creates a new SingletonServiceFactory.
        /// </summary>
        /// <param name="singleton">Singleton to return on each call to <see cref="CreateService"/>.</param>
        public SingletonServiceFactory(object singleton)
        {
            _singleton = singleton ?? throw new ArgumentNullException(nameof(singleton));
        }

        /// <inheritdoc />
        public void Initialize(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
        }

        /// <inheritdoc />
        public object CreateService(Type requestedType)
        {
            return _singleton;
        }
    }
}
