using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.DataMapping
{

    /// <summary>
    /// A class for mapping an input stream of JSON to a Type T using a <see cref="Newtonsoft.Json.JsonTextReader"/> instance.
    /// </summary>
    internal class JsonDataMapper : IDataMapper
    {
        private readonly ITypeSerializer _serializer;

        public JsonDataMapper(ITypeSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <summary>
        /// Maps a single row.
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        public T Map<T>(Stream stream) where T : class
        {
            return _serializer.Deserialize<T>(stream);
        }
    }
}
