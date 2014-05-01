using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    /// <summary>
    /// Provides and interface for mapping the results of a <see cref="ViewQuery"/> to it's <see cref="IViewResult{T}"/>
    /// </summary>
    internal interface IDataMapper
    {
        /// <summary>
        /// Maps a single row.
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        T Map<T>(Stream stream);

        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An collection typed to it's T Type value.</returns>
        List<T> MapAll<T>(Stream stream);
    }
}
