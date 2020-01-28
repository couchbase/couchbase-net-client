using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Implementation of <see cref="IServiceFactory"/> which constructs more specific types
    /// from a non-specific generic with the same number of type arguments. Keeps a singleton
    /// of each type to return on subsequent calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, new SingletonGenericServiceFactory(typeof(Logger&lt;&gt;)) could be registered
    /// against ILogger&lt;&gt;. A request for ILogger&lt;SomeType&gt; would return a singleton
    /// of the more specific implementation Logger&lt;SomeType&gt;.
    /// </para>
    /// <para>
    /// This factory must be registered against a generic type with the same number of type arguments
    /// and the same or stricter type argument constraints.
    /// </para>
    /// </remarks>
    internal class SingletonGenericServiceFactory : IServiceFactory
    {
        private readonly Type _genericType;
        private readonly ConcurrentDictionary<Type, object> _singletons = new ConcurrentDictionary<Type, object>();

        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Creates a new SingletonGenericServiceFactory.
        /// </summary>
        /// <param name="genericType">Non-specific generic type, i.e. Logger&lt;&gt; to construct.</param>
        /// <exception cref="ArgumentException">Not a generic type definition.</exception>
        public SingletonGenericServiceFactory(Type genericType)
        {
            _genericType = genericType ?? throw new ArgumentNullException(nameof(genericType));

            if (!genericType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Not a generic type definition.", nameof(genericType));
            }
        }

        /// <inheritdoc />
        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public object CreateService(Type requestedType)
        {
            return _singletons.GetOrAdd(requestedType, Factory);
        }

        private object Factory(Type requestedType)
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Not initialized");
            }

            var typeArgs = requestedType.GetGenericArguments();

            var type = _genericType.MakeGenericType(typeArgs);

            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(p => p.GetParameters().Length)
                .First();

            var constructorArgs = constructor.GetParameters()
                .Select(p => _serviceProvider.GetRequiredService(p.ParameterType))
                .ToArray();

            return constructor.Invoke(constructorArgs);
        }
    }
}
