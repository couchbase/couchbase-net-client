using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Views;

namespace Couchbase.Core.DataMapping
{
    /// <summary>
    /// Provides and interface for mapping the results of a <see cref="ViewQuery"/> to it's <see cref="IViewResult{TKey, TValue}"/>
    /// </summary>
    public interface IDataMapper
    {
        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{TKey, TValue}"/>'s Type parameter.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        T Map<T>(Stream stream) where T : class;

        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{TKey, TValue}"/>'s Type parameter.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        ValueTask<T> MapAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;
    }
}
