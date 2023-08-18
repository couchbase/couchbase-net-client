using System;
using System.Diagnostics.CodeAnalysis;
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
        private const string RequiresUnreferencedCodeWarning =
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
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return services.AddCouchbase(configuration.Bind);
        }

        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="options">Optional action to configure the <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        [RequiresUnreferencedCode(RequiresUnreferencedCodeWarning)]
        public static IServiceCollection AddCouchbase(this IServiceCollection services,
            Action<ClusterOptions>? options)
        {
            services.AddOptions();

            services.TryAddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.TryAddSingleton<IClusterProvider, ClusterProvider>();
            services.TryAddSingleton<IBucketProvider, BucketProvider>();

            // Register the logging configurator first so that the call to the options Action may override it
            services.AddTransient<IConfigureOptions<ClusterOptions>, LoggingConfigurator>();

            if (options != null)
            {
                services.Configure(options);
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
            services.AddCouchbaseBucket<T>(bucketName, null);

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
            where T: class, INamedBucketProvider
        {
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            services.AddSingleton(typeof(T), NamedBucketProxyGenerator.Instance.GetProxy(typeof(T), bucketName));

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(T), false);

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
        public static IServiceCollection AddCouchbaseBucket<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, Action<IBucketBuilder>? buildAction = null)
            where TService: class, INamedBucketProvider
            where TImplementation : class, TService
        {
            services.AddSingleton<TService, TImplementation>();

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(TService), false);

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
            services.TryAddCouchbaseBucket<T>(bucketName, null);

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
            where T : class, INamedBucketProvider
        {
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            services.TryAddSingleton(typeof(T), NamedBucketProxyGenerator.Instance.GetProxy(typeof(T), bucketName));

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(T), true);

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
        public static IServiceCollection TryAddCouchbaseBucket<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services, Action<IBucketBuilder>? buildAction = null)
            where TService : class, INamedBucketProvider
            where TImplementation : class, TService
        {
            services.TryAddSingleton<TService, TImplementation>();

            if (buildAction != null)
            {
                var builder = new BucketBuilder(services, typeof(TService), true);

                buildAction.Invoke(builder);
            }

            return services;
        }
    }
}
