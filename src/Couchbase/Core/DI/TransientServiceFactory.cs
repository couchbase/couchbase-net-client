using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Implementation of <see cref="IServiceFactory"/> which creates a transient
    /// service for each request.
    /// </summary>
    internal sealed class TransientServiceFactory : IServiceFactory
    {
        private readonly Func<IServiceProvider, object?> _factory;

        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Creates a new TransientServiceFactory which uses a lambda to create the service.
        /// </summary>
        /// <param name="factory">Lambda to invoke on each call to <see cref="CreateService"/>.</param>
        public TransientServiceFactory(Func<IServiceProvider, object?> factory)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (factory is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(factory));
            }

            _factory = factory;
        }

        /// <summary>
        /// Creates a new TransientServiceFactory which uses a type's constructor on each call to <see cref="CreateService"/>.
        /// </summary>
        /// <param name="type">Type to create on each call to <seealso cref="CreateService"/>.</param>
        public TransientServiceFactory([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
            : this(CreateFactory(type))
        {
        }

        /// <inheritdoc />
        public void Initialize(IServiceProvider serviceProvider)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (serviceProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public object? CreateService(Type requestedType)
        {
            if (_serviceProvider == null)
            {
                ThrowHelper.ThrowInvalidOperationException("Not initialized.");
            }

            return _factory(_serviceProvider);
        }

        private static Func<IServiceProvider, object> CreateFactory([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
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
