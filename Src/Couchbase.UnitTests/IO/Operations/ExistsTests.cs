using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class ExistsTests
    {
        public static IByteConverter Converter = new DefaultConverter();

        [Test]
        public void Exists_KeyNotFound_No_FramingExtras()
        {
            var buffer = new byte[]
            {
                129, 146, 0, 0, 0, 0, 0, 0, 0, 0, 0, 25, 0, 0, 0, 9, 0,
                0, 0, 0, 0, 0, 0, 0, 3, 118, 0, 12, 68, 79, 69, 83, 78,
                79, 84, 69, 88, 73, 83, 84, 128, 0, 0, 0, 0, 0, 0, 0, 0
            };

            var header = new OperationHeader
            {
                BodyLength = 25,
                FramingExtrasLength = 0,
            };
            var observe = new Observe("DOESNOTEXIST", new VBucket(null, 886, 1, null, 0, null, ""),
                new DefaultTranscoder(), 0);

            observe.Read(buffer, header);

            var result = observe.GetResultWithValue();
            Assert.AreEqual(KeyState.NotFound, result.Value.KeyState);
        }

        [Test]
        public void Exists_KeyNotFound_With_FramingExtras()
        {
            var buffer = new byte[]
            {
                24, 146, 3, 0, 0, 0, 0, 0, 0, 0, 0, 28, 0, 0, 0,
                9, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 14, 3, 118, 0, 12,
                68, 79, 69, 83, 78, 79, 84, 69, 88, 73, 83, 84, 128,
                0, 0, 0, 0, 0, 0, 0, 0
            };

            var header = new OperationHeader
            {
                BodyLength = 25,
                FramingExtrasLength = 3,
            };
            var observe = new Observe("DOESNOTEXIST", new VBucket(null, 886, 1, null, 0, null, ""),
                new DefaultTranscoder(), 0);

            observe.Read(buffer, header);

            var result = observe.GetResultWithValue();
            Assert.AreEqual(KeyState.NotFound, result.Value.KeyState);
        }

        [Test]
        public void Exists_KeyFound_No_FramingExtras()
        {
            var buffer = new byte[]
            {
                129, 146, 0, 0, 0, 0, 0, 0, 0, 0, 0, 23, 0, 0,
                0, 9, 0, 0, 0, 0, 0, 0, 0, 0, 1, 105, 0, 10, 97,
                105, 114, 108, 105, 110, 101, 95, 49, 48, 1, 22,
                181, 170, 118, 136, 119, 0, 0,
            };

            var header = new OperationHeader
            {
                BodyLength = 25,
                FramingExtrasLength = 0,
            };
            var observe = new Observe("airline_10", new VBucket(null, 886, 1, null, 0, null, ""),
                new DefaultTranscoder(), 0);

            observe.Read(buffer, header);

            var result = observe.GetResultWithValue();
            Assert.AreEqual(KeyState.FoundPersisted, result.Value.KeyState);
        }

        [Test]
        public void Exists_KeyFound_With_FramingExtras()
        {
            var buffer = new byte[]
            {
                24,146, 3, 0, 0, 0, 0, 0, 0, 0, 0, 26, 0, 0, 0, 9, 0, 0, 0, 0, 0,
                0, 0, 0, 2, 0, 16, 1, 105, 0, 10, 97, 105, 114, 108, 105, 110, 101,
                95, 49, 48, 1, 22, 178, 174, 90, 98, 72, 0, 0
            };

            var header = new OperationHeader
            {
                BodyLength = 25,
                FramingExtrasLength = 3,
            };
            var observe = new Observe("airline_10", new VBucket(null, 886, 1, null, 0, null, ""),
                new DefaultTranscoder(), 0);

            observe.Read(buffer, header);

            var result = observe.GetResultWithValue();
            Assert.AreEqual(KeyState.FoundPersisted, result.Value.KeyState);
        }
    }
}
