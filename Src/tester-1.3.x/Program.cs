using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase;
using Enyim.Caching.Memcached;
using Wintellect;

namespace tester_1._3.x
{
    class Program
    {
        static CouchbaseClient Client = new CouchbaseClient();
        static void Main(string[] args)
        {
            int n = 100000;

            using (var timer = new OperationTimer())
            {
                //SynchronousInsert(Client, n);
                ParallerInsert(Client, n);
            }
            Console.Read();
        }

        static void SynchronousInsert(CouchbaseClient client, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var key = "key" + i;
                var value = "value" + i;

                var result = client.ExecuteStore(StoreMode.Set, key, value);

                if (result.Success)
                {
                    Console.WriteLine("Write Key: {0} - Value: {1}", key, value);
                    var result2 = client.ExecuteGet<string>(key);
                    if (result2.Success)
                    {
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result.Message);
                    }
                }
                else
                {
                    Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                }
            }
        }

        static void ParallerInsert(CouchbaseClient client, int n)
        {
            //var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            Parallel.For(0, n, /*options,*/ i =>
            {
                var key = "key" + i;
                var value = "value" + i;

                var result = client.ExecuteStore(StoreMode.Set, key, value);

                if (result.Success)
                {
                    Console.WriteLine("Write Key: {0} - Value: {1}", key, value);
                    var result2 = client.ExecuteGet<string>(key);
                    if (result2.Success)
                    {
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result.Message);
                    }
                }
                else
                {
                    Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                }
            });
        }
    }
}
