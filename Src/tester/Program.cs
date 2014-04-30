using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Wintellect;

namespace tester
{
    class Program
    {
        private static AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private static Cluster _cluster;
        static void Main(string[] args)
        {
            var config = new ClientConfiguration(new PoolConfiguration()
            {
                MaxSize = 10,
                MinSize = 10
            });

            Cluster.Initialize(config);
            _cluster = Cluster.Get();
            var bucket = _cluster.OpenBucket("default");
 
            int n = 100000;

            using (var timer = new OperationTimer())
            {
                //ThreadPoolInsert(bucket, n);
               //ThreadPoolInsert(bucket, n);
              //SynchronousInsert(bucket, n);
              //ParallerInsert(bucket, n);
                MultiThreaded(4, 100000, bucket);
            }
            Console.Read();
            _cluster.CloseBucket(bucket);

            
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

                    /*var result = bucket.Insert(key, value);

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

                var result = bucket.Insert(key, value);

                if (result.Success)
                {
                    Console.WriteLine("Write Key: {0} - Value: {1}", key, value);
                    var result2 = bucket.Get<string>(key);
                    if (result2.Success)
                    {
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
            }
        }

        static void ParallerInsert(IBucket bucket, int n)
        {
            var options = new ParallelOptions {MaxDegreeOfParallelism = 3};
            
            Parallel.For(0, n, options, i =>
            {
                var key = "key" + i;
                var value = "value" + i;

              /* var result = bucket.Insert(key, value);

                if (result.Success)
                {
                        Console.WriteLine("Write Key: {0} - Value: {1}", key, value);*/
                   var result2 = bucket.Get<string>(key);
                    if (result2.Success)
                    {
                        if (result2.Value != value) { throw new Exception();}
                        Console.WriteLine("Read Key: {0} - Value: {1}", key, result2.Value);
                    }
                    else
                    {
                        Console.WriteLine("Read Error: {0} - {1}", key, result2.Message);
                    }
               /* }
                else
                {
                    Console.WriteLine("Write Error: {0} - {1}", key, result.Message);
                }*/
            });
        }

        public static void MultiThreaded(int threadCount, int keys, IBucket bucket)
        {
            var threads = new Thread[threadCount];
            for (var i = 0; i < threadCount; i++)
            {
                var threadData = new ThreadData
                {
                    Keys = keys / threadCount,
                    Bucket = bucket,
                    Part = i,
                    ThreadCount = threadCount
                };

                threads[i]= new Thread(ThreadProc);
                threads[i].Start(threadData);
            }
            _resetEvent.WaitOne();
        }

        static void ThreadProc(object state)
        {
            var threadData = state as ThreadData;
            for (var i = 0; i < threadData.Keys; i++)
            {
                var key = "key" + i;
                var value = "value" + i;

               // var result = threadData.Bucket.Insert(key, value);
               // Console.WriteLine("Insert {0} - {1} on thread {2}", key, result.Success ? "success" : "failure", Thread.CurrentThread.ManagedThreadId);
                var result1 = threadData.Bucket.Get<string>(key);
                Console.WriteLine("Get {0} - {1} on thread {2}", key, result1.Success ? "success" : "failure", Thread.CurrentThread.ManagedThreadId);
            }

            if (threadData.ThreadCount/threadData.Keys == threadData.Part)
            {
                _resetEvent.Set();
            }
        }

        public class ThreadData
        {
            public int Keys;
            public IBucket Bucket;
            public int Part;
            public int ThreadCount;
        }
    }
}
