using System;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Implementation of <see cref="IServiceFactory"/> which uses a lambda to construct the service.
    /// </summary>
    internal class LambdaServiceFactory : IServiceFactory
    {
        private readonly Func<IServiceProvider, object?> _factory;

        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Creates a new LambdaServiceFactory.
        /// </summary>
        /// <param name="factory">Lambda to invoke on each call to <seealso cref="CreateService"/>.</param>
        public LambdaServiceFactory(Func<IServiceProvider, object?> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc />
        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public object? CreateService(Type requestedType)
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Not initialized.");
            }

            return _factory(_serviceProvider);
        }
    }
}
