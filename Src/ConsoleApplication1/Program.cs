using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var p = new Program(); p.button1_Click(null, null);
            Console.Read();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CouchbaseCluster cluster = InitializeCluster2();
            var bucket = GetBucket(cluster, "NewUserLink");
        }

        private CouchbaseCluster InitializeCluster2()
        {
            //Create a custom configuration specifiy a max pool size of 10
            var configuration = new ClientConfiguration();
            configuration.PoolConfiguration.MaxSize = 10;
            configuration.PoolConfiguration.MinSize = 10;

            configuration.Servers.Clear();
            configuration.Servers.Add(new Uri("http://192.168.56.102:8091/pools"));

            var bc = new BucketConfiguration();
            bc.Password = "mypass";
            bc.Username = "admin";
            bc.BucketName = "NewUserLink";

            bc.Servers.Clear();
            bc.Servers.Add(new Uri("http://192.168.56.102:8091/pools"));
            bc.Port = 11211;

            configuration.BucketConfigs.Clear();
            configuration.BucketConfigs.Add("NewUserLink", bc);

            //Initialize the cluster using the default configuration
            CouchbaseCluster.Initialize(configuration);

            //Get an instance of the cluster;
            return CouchbaseCluster.Get();
        }

        private IBucket GetBucket(CouchbaseCluster cluster, string bucketName)
        {
            //Open the default bucket
            return cluster.OpenBucket(bucketName);
        }
    }
}
