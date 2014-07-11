using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Wintellect;

namespace tester
{
    class Program
    {
        private static readonly AutoResetEvent ResetEvent = new AutoResetEvent(false);
        private static CouchbaseCluster _cluster;
        static void Main(string[] args)
        {
            File.Delete(@"C:\temp\log.txt");
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://127.0.0.1:8091/pools")
                },
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 2,
                    MinSize = 1
                },
                UseSsl = false
            };

            using (_cluster = new CouchbaseCluster(config))
            {
                using (var bucket = _cluster.OpenBucket("default"))
                {
                    const int n = 100000;
                    using (var timer = new OperationTimer())
                    {
                        //ThreadPoolInsert(bucket, n);
                        //ThreadPoolInsert(bucket, n);
                        //SynchronousInsert(bucket, n);
                        //ParallerInsert(bucket, n);
                        MultiThreaded(8, n, bucket);
                    }
                }
            }
            Console.Read();
        }

        static void ThreadPoolInsert(IBucket bucket, int n)
        {
            for (int i = 0; i < n; i++)
            {
                int i1 = i;
                Task.Factory.StartNew(() =>
                {
                    var key = "key" + i1;
                    var value = "value" + i1;

                    /*var result = bucket.Upsert(key, value);

                    if (result.Success)
                    {
                        Console.WriteLine("Write Key: {0} - Value: {1}", key, value);*/
                    var result2 = bucket.Get<string>(key);
                    if (result2.Success)
                    {
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result2.Message);
                    }
                    /*}
                    else
                    {
                        Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                    }*/
                });
            }
        }

        static void SynchronousInsert(IBucket bucket, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var key = "key" + i;
                var value = "value" + i;

               /* var result = bucket.Upsert(key, value);
                if (result.Success)
                {
                    Console.WriteLine("Write Key: {0} - Value: {1}", key, value);*/
                    var result2 = bucket.Get<string>(key);
                    if (result2.Success)
                    {
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result2.Message);
                    }
                /*}
                else
                {
                    Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                }*/
            }
        }

        static void ParallerInsert(IBucket bucket, int n)
        {
            var options = new ParallelOptions {MaxDegreeOfParallelism = 4};
            Parallel.For(0, n, options, i =>
            {
                var key = "key" + i;
                int value =  i;

              var result = bucket.Upsert(key, value);

                if (result.Success)
                {
                    Console.WriteLine("Write Key: {0} - Value: {1}", key, value);
                   
                    var result2 = bucket.Get<int>(key);
                    if (result2.Success)
                    {
                        if (result2.Value != value)
                        {
                            throw new Exception();
                        }
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result2.Message);
                    }
               }
                else
                {
                    Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                }
               
            });
        }

        public static void MultiThreaded(int threadCount, int keys, IBucket bucket)
        {
            var threads = new Thread[threadCount];
            for (var i = 0; i < threadCount; i++)
            {
                var threadData = new ThreadData
                {
                    NumberOfKeysToCreate = keys,
                    Keys = keys / threadCount,
                    Bucket = bucket,
                    Part = i,
                    ThreadCount = threadCount
                };

                threads[i]= new Thread(ThreadProc);
                threads[i].Start(threadData);
            }
            ResetEvent.WaitOne();
        }

        static void ThreadProc(object state)
        {
            var threadData = state as ThreadData;
            for (var i = 0; i < threadData.Keys; i++)
            {
                var key = "key" + i;
                var value = "value" + i;

                var result = threadData.Bucket.Upsert(key, value);
                Console.WriteLine("Upsert {0} - {1} on thread {2}", key, result.Success ? "success" : "failure", Thread.CurrentThread.ManagedThreadId);
                var result1 = threadData.Bucket.Get<string>(key);
                Console.WriteLine("Get {0} - {1} on thread {2}: {3} reason: {4}", key, result1.Success ? "success" : "failure", Thread.CurrentThread.ManagedThreadId, result1.Value, result1.Message);
                if(value != result1.Value) throw new Exception();
            }

            ThreadData.Processed += threadData.Keys;
            if (ThreadData.Processed == threadData.NumberOfKeysToCreate)
            {
                ResetEvent.Set();
            }
        }

        public class ThreadData
        {
            public int NumberOfKeysToCreate;
            public static volatile int Processed;
            public int Keys;
            public IBucket Bucket;
            public int Part;
            public int ThreadCount;
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
