using System;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions to <see cref="IServiceCollection"/> for Couchbase dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add Couchbase dependencies to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configuration">Section from the configuration that can be bound to <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
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
        public static IServiceCollection AddCouchbase(this IServiceCollection services,
            Action<ClusterOptions> options)
        {
            services.AddOptions();

            services.AddSingleton<NamedBucketProxyGenerator>();
            services.TryAddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.TryAddScoped<IClusterProvider, ClusterProvider>();
            services.TryAddScoped<IBucketProvider, BucketProvider>();

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
        public static IServiceCollection AddCouchbaseBucket<T>(this IServiceCollection services, string bucketName)
            where T: class, INamedBucketProvider
        {
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            services.AddSingleton(serviceProvider =>
            {
                var generator = serviceProvider.GetRequiredService<NamedBucketProxyGenerator>();

                return generator.GetProxy<T>(serviceProvider.GetRequiredService<IBucketProvider>(), bucketName);
            });

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
        public static IServiceCollection TryAddCouchbaseBucket<T>(this IServiceCollection services, string bucketName)
            where T : class, INamedBucketProvider
        {
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            services.TryAddSingleton(serviceProvider =>
            {
                var generator = serviceProvider.GetRequiredService<NamedBucketProxyGenerator>();

                return generator.GetProxy<T>(serviceProvider.GetRequiredService<IBucketProvider>(), bucketName);
            });

            return services;
        }
    }
}
