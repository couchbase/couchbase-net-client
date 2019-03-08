using System;
using System.Threading.Tasks;

namespace Couchbase.Runner
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new Configuration().
                WithServers("http://10.144.191.101:8091").
                WithBucket("default").
                WithCredentials("Administrator", "password");

            var cluster = new Cluster();
            cluster.ConnectAsync(config).ConfigureAwait(false).GetAwaiter().GetResult();

            var bucket = cluster.GetBucket("default");
            bucket.LoadManifest("manifest.json");

            var coll = bucket.GetCollection("_default", "_default");

            coll.Get<dynamic>("id", options => { options.Timeout = new TimeSpan(0, 0, 30);});

            //0x7f1
            var set = coll.Insert(new Document<string>
            {
                Id = "Hello",
                Content = "World"
            });


            

            var get = bucket.Get<dynamic>("Hello");
            var result = get.Result;
        }
    }
}
