using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class MultiGetTests : OperationTestBase
    {
        [Test]
        public void Test()
        {
            var keyValues = new Dictionary<string, string>();
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    for (int i = 0; i <10; i++)
                    {
                        var key = "MGETKEY" + i;
                        var value = "{\"val:\"MGETVALUE" + i +"\"}";
                        bucket.Upsert(key, value);
                        keyValues.Add(key, value);
                    }

                    foreach (var keyValue in keyValues)
                    {
                        Console.WriteLine(bucket.Get<dynamic>(keyValue.Key).Value);
                    }
                }
            }

            var operations = new ConcurrentDictionary<uint, IOperation>();
            foreach (var keyValue in keyValues)
            {
                var getk = new GetK<dynamic>(keyValue.Key, GetVBucket(), Converter, Transcoder);
                operations.TryAdd(getk.Opaque, getk);
            }
            var noop = new Noop(Converter);
            operations.TryAdd(noop.Opaque, noop);

            var results = IOStrategy.Execute<dynamic>(operations);
        }

        [Test]
        public void Test2()
        {
            var keys = new List<string>();
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket() as CouchbaseBucket)
                {
                    for (int i = 0; i <25; i++)
                    {
                        var key = "MGETKEY" + i;
                        var value = "{\"val:\"MGETVALUE" + i + "\"}";
                        bucket.Upsert(key, value);
                        keys.Add(key);
                    }

                    var results = bucket.Get2<dynamic>(keys);
                    foreach (var operationResult in results)
                    {
                        Console.WriteLine("{0} {1} {2} {3}", operationResult.Success, operationResult.Message, operationResult.Status, operationResult.Value);
                    }
                }
            }
        }

        [Test]
        public void Test_Parsing()
        {
            var bytes = new byte[]
            {
                129, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 23, 0, 0, 0, 7, 19, 187, 150, 150, 178, 107, 16, 184, 4, 0, 0, 18,
                123, 34, 118, 97, 108, 58, 34, 77, 71, 69, 84, 86, 65, 76, 85, 69, 48, 34, 125, 129, 0, 0, 0, 4, 0, 0, 0,
                0, 0, 0, 23, 0, 0, 0, 8, 19, 187, 150, 150, 178, 136, 224, 193, 4, 0, 0, 18, 123, 34, 118, 97, 108, 58,
                34, 77, 71, 69, 84, 86, 65, 76, 85, 69, 49, 34, 125
            };

            var responses = new List<Tuple<ArraySegment<byte>, ArraySegment<byte>>>();
            var nextPosition = 0;
            var first = true;
            while (true)
            {
                if (first)
                {
                    var header = new ArraySegment<byte>(bytes, 0, 24);
                    var bodyLength = Converter.ToInt32(header.Array, HeaderIndexFor.Body);
                    var body = new ArraySegment<byte>(bytes, 24, bodyLength);
                    responses.Add(new Tuple<ArraySegment<byte>, ArraySegment<byte>>(header, body));
                    nextPosition = bodyLength + 24;
                    first = false;
                }
                var nextheader = new ArraySegment<byte>(bytes, nextPosition, 24);
                var nextBodyLength = Converter.ToInt32(nextheader.Array, HeaderIndexFor.Body);
                var nextBody = new ArraySegment<byte>(bytes, 24, nextBodyLength);
                responses.Add(new Tuple<ArraySegment<byte>, ArraySegment<byte>>(nextheader,nextBody));
                nextPosition += nextBodyLength + 24;
                if (nextPosition >= bytes.Length)
                {
                    break;
                }
            }
        }

        [Test]
        public void Test_Parsing2()
        {
            var bytes = new byte[]
            {
                129, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 23, 0, 0, 0, 7, 19, 187, 150, 150, 178, 107, 16, 184, 4, 0, 0, 18,
                123, 34, 118, 97, 108, 58, 34, 77, 71, 69, 84, 86, 65, 76, 85, 69, 48, 34, 125, 129, 0, 0, 0, 4, 0, 0, 0,
                0, 0, 0, 23, 0, 0, 0, 8, 19, 187, 150, 150, 178, 136, 224, 193, 4, 0, 0, 18, 123, 34, 118, 97, 108, 58,
                34, 77, 71, 69, 84, 86, 65, 76, 85, 69, 49, 34, 125
            };

            var responses = new List<Tuple<uint, MemoryStream>>();

            var bodyLength = 0;
            var opaque = 0u;
            var nextPosition = 0;
            var first = true;
            while (true)
            {
                if (first)
                {
                    bodyLength = Converter.ToInt32(bytes, HeaderIndexFor.Body);
                    opaque = Converter.ToUInt32(bytes, HeaderIndexFor.Opaque);
                    responses.Add(new Tuple<uint, MemoryStream>(opaque, new MemoryStream(bytes, 0, bodyLength + 24)));
                    nextPosition = bodyLength + 24;
                    first = false;
                }
                bodyLength = Converter.ToInt32(bytes, nextPosition + HeaderIndexFor.Body);
                opaque = Converter.ToUInt32(bytes, nextPosition + HeaderIndexFor.Opaque);
                responses.Add(new Tuple<uint, MemoryStream>(opaque, new MemoryStream(bytes, nextPosition, bodyLength + 24)));
                nextPosition += bodyLength + 24;
                if (nextPosition >= bytes.Length)
                {
                    break;
                }
            }
        }
    }
}
