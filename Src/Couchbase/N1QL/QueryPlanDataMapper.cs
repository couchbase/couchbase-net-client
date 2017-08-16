using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Couchbase.Views;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Prepare in QueryClient has a model with a hard reference to Newtonsoft (QueryPlan).
    /// This class makes sure it gets deserialized with the DefaultSerializer instead of the
    /// one passed in via the ClientConfiguration.
    /// </summary>
    internal class QueryDataMapper : IDataMapper
    {
        private readonly ITypeSerializer _serializer;

        public QueryDataMapper()
        {
            _serializer = SerializerFactory.GetSerializer()();
        }

        /// <summary>
        /// Maps a single row.
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        public T Map<T>(Stream stream) where T : class
        {
            return _serializer.Deserialize<T>(stream);
        }
    }
}
