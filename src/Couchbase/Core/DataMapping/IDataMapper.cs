using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Views;

namespace Couchbase.Core.DataMapping
{
    /// <summary>
    /// Provides and interface for mapping the results of a <see cref="ViewQuery"/> to it's <see cref="IViewResult"/>
    /// </summary>
    public interface IDataMapper
    {
        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        T Map<T>(Stream stream) where T : class;
    }
}
