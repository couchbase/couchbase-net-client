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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion