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
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            Parallel.For(0, n, options, i =>
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
