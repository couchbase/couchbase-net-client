using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Views
{
    /// <summary>
    /// A class for mapping an input stream of JSON to a Type T using a <see cref="Newtonsoft.Json.JsonTextReader"/> instance.
    /// </summary>
    internal class JsonDataMapper : IDataMapper
    {
        private readonly JsonSerializer _serializer;

        public  JsonDataMapper()
        {
            _serializer = new JsonSerializer();
        }

        /// <summary>
        /// Maps a single row.
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        public T Map<T>(Stream stream)
        {
            T instance;
            using (var streamReader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    instance = _serializer.Deserialize<T>(jsonReader);
                }
            }
            return instance;
        }

        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{T}"/>'s Type paramater.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An collection typed to it's T Type value.</returns>
        public List<T> MapAll<T>(Stream stream)
        {
            List<T> instances;
            using (var streamReader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    instances = _serializer.Deserialize<List<T>>(jsonReader);
                }
            }
            return instances;
        }
    }
}
