using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Couchbase.Utils;

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
    /// <para>
    /// For trimming compatibility, it is imperative that the interface registered as the service in DI
    /// have DynamicallyAccessedMembers annotations on the type arguments that match the ones on the concrete
    /// implementation. For example, if <c>interface IMyInterface&lt;T&gt;</c> is the type being requested from DI and the concrete
    /// implementation passed to this factory is <c>class MyClass&lt;[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T&gt;</c>
    /// then <c>IMyInterface&lt;T&gt;</c> must have the same annotation on the type argument <c>T</c>.
    /// See https://github.com/dotnet/runtime/blob/7c00b17be1b2ffb6ed49ad68cf36e9a056323152/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/CallSiteFactory.cs#L94-L98
    /// </para>
    /// </remarks>
    internal class SingletonGenericServiceFactory : IServiceFactory
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        private readonly Type _genericType;
        private readonly ConcurrentDictionary<Type, object> _singletons = new ConcurrentDictionary<Type, object>();

        private IServiceProvider? _serviceProvider;
        private Func<Type, object>? _factoryDelegateCache;

        /// <summary>
        /// Creates a new SingletonGenericServiceFactory.
        /// </summary>
        /// <param name="genericType">Non-specific generic type, i.e. Logger&lt;&gt; to construct.</param>
        /// <exception cref="ArgumentException">Not a generic type definition.</exception>
        public SingletonGenericServiceFactory([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type genericType)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (genericType is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(genericType));
            }
            if (!genericType.IsGenericTypeDefinition)
            {
                ThrowHelper.ThrowArgumentException("Not a generic type definition.", nameof(genericType));
            }

            _genericType = genericType;
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
        public object CreateService(Type requestedType)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (requestedType is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(requestedType));
            }

            return _singletons.GetOrAdd(requestedType, _factoryDelegateCache ??= Factory);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2055",
            Justification = "The generic interface type arguments should have matching DynamicallyAccessedMembers to the implementation's type arguments.")]
        private object Factory(Type requestedType)
        {
            if (_serviceProvider == null)
            {
                ThrowHelper.ThrowInvalidOperationException("Not initialized");
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
