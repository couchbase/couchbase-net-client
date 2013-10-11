using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Enyim;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class StatusCodeTests
    {
        private static CouchbaseClient s_cbClient;
        private static byte[] s_bigData = new byte[524288];
        private static int s_keyCounter = 0;
        private static TimeSpan s_validity = TimeSpan.FromMinutes(1);

        private const int THREADS = 100;
        private const int LOOPS = 100;

        [Test]
        public void Test_That_Starved_SocketPool_Sends_StatusCode_SocketPoolTimeout()
        {
            try
            {
                s_cbClient = new CouchbaseClient("socket-timeout");

                List<Thread> workers = new List<Thread>();
                for (int i = 0; i < THREADS; i++)
                {
                    Thread t = new Thread(ThreadBody);
                    t.Priority = ThreadPriority.BelowNormal;
                    workers.Add(t);
                }
                foreach (Thread t in workers)
                {
                    t.Join();
                }
                Console.WriteLine();
                Console.WriteLine("done");
                Thread.Sleep(1000);
            }
            finally
            {
                s_cbClient.Dispose();
            }
        }

        private static void ThreadBody()
        {
            for (int i = 0; i < LOOPS; i++)
            {
                var result = s_cbClient.ExecuteStore(StoreMode.Set, Interlocked.Increment(ref s_keyCounter).ToString(), s_bigData, s_validity);
                if (!result.Success)
                {
                    Assert.AreEqual(result.StatusCode, StatusCode.SocketPoolTimeout, "StatusCode is SocketTimeout");
                }
                else
                {
                    Debug.WriteLine("Success");
                }
            }
        }
    }
}
