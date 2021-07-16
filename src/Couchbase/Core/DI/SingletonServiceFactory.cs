using System;
using System.Linq;
using System.Reflection;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Implementation of <see cref="IServiceFactory"/> which always returns the same singleton.
    /// </summary>
    internal class SingletonServiceFactory : IServiceFactory
    {
        private readonly Func<IServiceProvider, object>? _lambda;
        private IServiceProvider? _serviceProvider;
        private object? _singleton;

        /// <summary>
        /// Creates a new SingletonServiceFactory with a preexisting object.
        /// </summary>
        /// <param name="singleton">Singleton to return on each call to <see cref="CreateService"/>.</param>
        public SingletonServiceFactory(object singleton)
        {
            _singleton = singleton ?? throw new ArgumentNullException(nameof(singleton));
        }

        /// <summary>
        /// Creates a new SingletonServiceFactory with an object of a specific type.
        /// </summary>
        /// <param name="implementationType">Implementation type.</param>
        /// <remarks>
        /// Delays construction until the first request for the type.
        /// Uses the constructor with the most parameters.
        /// </remarks>
        public SingletonServiceFactory(Type implementationType)
            : this(CreateFactory(implementationType))
        {
        }

        /// <summary>
        /// Creates a new SingletonServiceFactory which uses a lambda to create the object on the first request.
        /// </summary>
        /// <param name="lambda">Lambda function which creates the object.</param>
        public SingletonServiceFactory(Func<IServiceProvider, object> lambda)
        {
            _lambda = lambda ?? throw new ArgumentNullException(nameof(lambda));
        }

        /// <inheritdoc />
        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public object CreateService(Type requestedType)
        {
            return _singleton ??= _lambda!.Invoke(_serviceProvider ?? throw new InvalidOperationException("Not initialized."));
        }

        private static Func<IServiceProvider, object> CreateFactory(Type implementationType)
        {
            var constructor = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(p => p.GetParameters().Length)
                .First();

            object Factory(IServiceProvider serviceProvider)
            {
                var constructorArgs = constructor.GetParameters()
                    .Select(p => serviceProvider.GetRequiredService(p.ParameterType))
                    .ToArray();

                return constructor.Invoke(constructorArgs);
            }

            return Factory;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
