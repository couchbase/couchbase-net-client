using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Couchbase.Configuration.Server.Serialization;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.FileSystem
{
    internal class FileSystemConfig : IServerConfig
    {
        private readonly string _path;

        public FileSystemConfig(string path)
        {
            _path = path;
            StreamingHttp = new List<BucketConfig>();
        }

        public Pools Pools { get; private set; }

        public List<BucketConfig> Buckets { get; private set; }

        public Bootstrap Bootstrap { get; private set; }

        public List<BucketConfig> StreamingHttp { get; set; }

        public void Initialize()
        {
            Bootstrap = Deserialize<Bootstrap>(_path);
            Pools = Deserialize<Pools>(Bootstrap.Pools.First().Uri);
            Buckets = Deserialize<List<BucketConfig>>(Pools.Buckets.Uri);
            foreach (BucketConfig bucket in Buckets)
            {
                StreamingHttp.Add(Deserialize<BucketConfig>(bucket.StreamingUri));
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public Uri BootstrapServer
        {
            get { throw new NotImplementedException(); }
        }

        private static T Deserialize<T>(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
        }
    }
}