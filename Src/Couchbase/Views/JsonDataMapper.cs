using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Views
{
    internal class JsonDataMapper : IDataMapper
    {
        private readonly JsonSerializer _serializer;

        public  JsonDataMapper()
        {
            _serializer = new JsonSerializer();
        }

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
