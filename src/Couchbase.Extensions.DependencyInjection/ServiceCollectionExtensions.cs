using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions to <see cref="IServiceCollection"/> for Couchbase dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        internal const string RequiresUnreferencedCodeWarning =
            "The Couchbase SDK is not compatible with trimming.";
        internal const string RequiresDynamicCodeWarning =
            "Dynamically generated INamedBucketProvider or INamedCollectionProvider instances require dynamic code and are not compatible with AOT. Use an overload that accepts a concrete implementation type instead.";

        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configuration">Section from the configuration that can be bound to <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresUnreferencedCode(RequiresUnreferencedCodeWarning)]
        public static IServiceCollection AddCouchbase(this IServiceCollection services, IConfiguration configuration)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (configuration == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(configuration));
            }

            return services.AddKeyedCouchbase(serviceKey: null, configuration.Bind);
        }

        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="configuration">Section from the configuration that can be bound to <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresUnreferencedCode(RequiresUnreferencedCodeWarning)]
        public static IServiceCollection AddKeyedCouchbase(this IServiceCollection services, string? serviceKey, IConfiguration configuration)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (configuration == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(configuration));
            }

            return services.AddKeyedCouchbase(serviceKey, configuration.Bind);
        }

        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="options">Optional action to configure the <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresUnreferencedCode(RequiresUnreferencedCodeWarning)]
        public static IServiceCollection AddCouchbase(this IServiceCollection services,
            Action<ClusterOptions>? options) =>
            services.AddKeyedCouchbase(serviceKey: null, options);

        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="options">Optional action to configure the <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresUnreferencedCode(RequiresUnreferencedCodeWarning)]
        public static IServiceCollection AddKeyedCouchbase(this IServiceCollection services,
            string? serviceKey, Action<ClusterOptions>? options)
        {
            services.AddOptions();

            services.TryAddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>(serviceKey);

            var clusterService = ServiceDescriptor.KeyedSingleton<IClusterProvider, ClusterProvider>(serviceKey);
            if (!IsServiceRegistered(services, clusterService))
            {
                services.Add(clusterService);

                // Forwards requests for IBucketProvider to the combined IClusterProvider/IBucketProvider implementation.
                // We don't want to register it directly because we want the same singleton to be returned for both.
                services.TryAddKeyedSingleton(serviceKey, static (serviceProvider, serviceKey) =>
                    (IBucketProvider)serviceProvider.GetRequiredKeyedService<IClusterProvider>(serviceKey));
            }
            else
            {
                // ICluster provider is already registered, so try to register the LegacyBucketProvider.
                services.TryAddKeyedSingleton<IBucketProvider, LegacyBucketProvider>(serviceKey);
            }

            var loggingService = ServiceDescriptor.Transient<IConfigureOptions<ClusterOptions>, LoggingConfigurator>();
            if (!IsServiceRegistered(services, loggingService, matchImplementationType: true))
            {
                // Register the logging configurator first so that the call to the options Action may override it.
                // However, it is shared across all keyed clusters, so only register it once.
                services.Add(loggingService);
            }

            if (options != null)
            {
                services.Configure(serviceKey ?? Options.DefaultName, options);
            }

            return services;
        }

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection AddCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string bucketName)
            where T : class, INamedBucketProvider =>
            services.AddKeyedCouchbaseBucket<T>(serviceKey: null, bucketName, buildAction: null);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection AddKeyedCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string? serviceKey, string bucketName)
            where T : class, INamedBucketProvider =>
            services.AddKeyedCouchbaseBucket<T>(serviceKey, bucketName, buildAction: null);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection AddCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string bucketName, Action<IBucketBuilder>? buildAction)
            where T: class, INamedBucketProvider =>
            services.AddKeyedCouchbaseBucket<T>(serviceKey: null, bucketName, buildAction);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection AddKeyedCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string? serviceKey, string? bucketName, Action<IBucketBuilder>? buildAction)
            where T: class, INamedBucketProvider
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketName == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketName));
            }

            services.AddKeyedSingleton(typeof(T), serviceKey,
                NamedBucketProxyGenerator.Instance.GetProxy(typeof(T), serviceKey, bucketName));

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(T), serviceKey, false);

                buildAction.Invoke(builder);
            }

            return services;
        }

        /// <summary>
        /// Register an bucket via an interface inherited from <see cref="INamedBucketProvider"/> and a
        /// concrete implementation of that interface.
        /// </summary>
        /// <typeparam name="TService">Interface inherited from <see cref="INamedBucketProvider"/>.</typeparam>
        /// <typeparam name="TImplementation">Concrete implementation of <typeparamref name="TService"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        public static IServiceCollection AddCouchbaseBucket<TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, Action<IBucketBuilder>? buildAction = null)
            where TService : class, INamedBucketProvider
            where TImplementation : class, TService =>
            services.AddKeyedCouchbaseBucket<TService, TImplementation>(serviceKey: null, buildAction);

        /// <summary>
        /// Register an bucket via an interface inherited from <see cref="INamedBucketProvider"/> and a
        /// concrete implementation of that interface.
        /// </summary>
        /// <typeparam name="TService">Interface inherited from <see cref="INamedBucketProvider"/>.</typeparam>
        /// <typeparam name="TImplementation">Concrete implementation of <typeparamref name="TService"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        public static IServiceCollection AddKeyedCouchbaseBucket<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, string? serviceKey, Action<IBucketBuilder>? buildAction = null)
            where TService: class, INamedBucketProvider
            where TImplementation : class, TService
        {
            services.AddKeyedSingleton<TService, TImplementation>(serviceKey);

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(TService), serviceKey, false);

                buildAction.Invoke(builder);
            }

            return services;
        }

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name if the interface hasn't already been added.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection TryAddCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string bucketName)
            where T : class, INamedBucketProvider =>
            services.TryAddKeyedCouchbaseBucket<T>(serviceKey: null, bucketName, buildAction: null);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name if the interface hasn't already been added.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection TryAddKeyedCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string? serviceKey, string bucketName)
            where T : class, INamedBucketProvider =>
            services.TryAddKeyedCouchbaseBucket<T>(serviceKey, bucketName, buildAction: null);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name if the interface hasn't already been added.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection TryAddCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string bucketName, Action<IBucketBuilder>? buildAction)
            where T : class, INamedBucketProvider =>
            services.TryAddKeyedCouchbaseBucket<T>(serviceKey: null, bucketName, buildAction);

        /// <summary>
        /// Register an interface based on <see cref="INamedBucketProvider"/> which will be injected
        /// with a specific bucket name if the interface hasn't already been added.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedBucketProvider"/>.  Must not add any members.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="bucketName">The name of the Couchbase bucket.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(RequiresDynamicCodeWarning)]
        public static IServiceCollection TryAddKeyedCouchbaseBucket<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IServiceCollection services, string? serviceKey, string bucketName, Action<IBucketBuilder>? buildAction)
            where T : class, INamedBucketProvider
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketName == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketName));
            }

            services.TryAddKeyedSingleton(typeof(T), serviceKey,
                NamedBucketProxyGenerator.Instance.GetProxy(typeof(T), serviceKey, bucketName));

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(T), serviceKey, true);

                buildAction.Invoke(builder);
            }

            return services;
        }

        /// <summary>
        /// Register a bucket via an interface inherited from <see cref="INamedBucketProvider"/> and a
        /// concrete implementation of that interface.
        /// </summary>
        /// <typeparam name="TService">Interface inherited from <see cref="INamedBucketProvider"/>.</typeparam>
        /// <typeparam name="TImplementation">Concrete implementation of <typeparamref name="TService"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection TryAddCouchbaseBucket<TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, Action<IBucketBuilder>? buildAction = null)
            where TService : class, INamedBucketProvider
            where TImplementation : class, TService =>
            services.TryAddKeyedCouchbaseBucket<TService, TImplementation>(serviceKey: null, buildAction);


        /// <summary>
        /// Register a bucket via an interface inherited from <see cref="INamedBucketProvider"/> and a
        /// concrete implementation of that interface.
        /// </summary>
        /// <typeparam name="TService">Interface inherited from <see cref="INamedBucketProvider"/>.</typeparam>
        /// <typeparam name="TImplementation">Concrete implementation of <typeparamref name="TService"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">Service key to uniquely represent the cluster.</param>
        /// <param name="buildAction">Action to perform further configuration.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection TryAddKeyedCouchbaseBucket<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, string? serviceKey, Action<IBucketBuilder>? buildAction = null)
            where TService : class, INamedBucketProvider
            where TImplementation : class, TService
        {
            services.TryAddKeyedSingleton<TService, TImplementation>(serviceKey);

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(TService), serviceKey, true);

                buildAction.Invoke(builder);
            }

            return services;
        }

        #region Helpers

        private static bool IsServiceRegistered(IServiceCollection services, ServiceDescriptor serviceDescriptor,
            bool matchImplementationType = false)
        {
            var count = services.Count;
            for (var i = 0; i < count; i++)
            {
                if (services[i].ServiceType == serviceDescriptor.ServiceType
                    && services[i].ServiceKey == serviceDescriptor.ServiceKey
                    && (!matchImplementationType || services[i].ImplementationType == serviceDescriptor.ImplementationType))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
