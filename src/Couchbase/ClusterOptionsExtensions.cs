using System;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Extensions for <see cref="ClusterOptions"/>.
    /// </summary>
    public static class ClusterOptionsExtensions
    {
        /// <summary>
        /// Register a transient service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddTransientService<TService, TImplementation>(
            this ClusterOptions clusterOptions)
            where TImplementation : TService =>
            clusterOptions.AddService<TService, TImplementation>(ClusterServiceLifetime.Transient);

        /// <summary>
        /// Register a transient service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <param name="factory">Factory which creates the service each time it is requested.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddTransientService<TService, TImplementation>(
            this ClusterOptions clusterOptions,
            Func<IServiceProvider, TImplementation> factory)
            where TImplementation : notnull, TService =>
            clusterOptions.AddService<TService, TImplementation>(factory, ClusterServiceLifetime.Transient);

        /// <summary>
        /// Register a singleton service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="T">The type of the service which will be requested and returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <param name="singleton">Singleton instance which is always returned.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddClusterService<T>(
            this ClusterOptions clusterOptions,
            T singleton)
            where T : notnull =>
            clusterOptions.AddService<T, T>(_ => singleton, ClusterServiceLifetime.Cluster);

        /// <summary>
        /// Register a singleton service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <param name="singleton">Singleton instance which is always returned.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddClusterService<TService, TImplementation>(
            this ClusterOptions clusterOptions,
            TImplementation singleton)
            where TImplementation : notnull, TService =>
            clusterOptions.AddService<TService, TImplementation>(_ => singleton, ClusterServiceLifetime.Cluster);

        /// <summary>
        /// Register a service with the cluster's <see cref="ICluster.ClusterServices"/> using the cluster's lifetime.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddClusterService<TService, TImplementation>(
            this ClusterOptions clusterOptions)
            where TImplementation : TService =>
            clusterOptions.AddService<TService, TImplementation>(ClusterServiceLifetime.Cluster);

        /// <summary>
        /// Register a service with the cluster's <see cref="ICluster.ClusterServices"/> using the cluster's lifetime.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="clusterOptions">The <see cref="ClusterOptions"/>.</param>
        /// <param name="factory">Factory which creates the service the first time it is requested.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public static ClusterOptions AddClusterService<TService, TImplementation>(
            this ClusterOptions clusterOptions,
            Func<IServiceProvider, TImplementation> factory)
            where TImplementation : notnull, TService =>
            clusterOptions.AddService<TService, TImplementation>(factory, ClusterServiceLifetime.Cluster);
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
