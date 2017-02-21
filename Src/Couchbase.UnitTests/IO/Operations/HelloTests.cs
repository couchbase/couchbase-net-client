using System;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

#if !NET45
using System.Runtime.InteropServices;
#endif

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class HelloTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new Hello(key, new short[] { }, null, 0, 0), "Key cannot be empty.");
        }

        [Test]
        public void Key_Is_Sent_In_Packet()
        {
            const string key = "test-key";
            var transcoder = new DefaultTranscoder();

            var hello = new Hello(key, new short[0], transcoder, 0, 0);
            var bytes = hello.Write();

            // key comes after header (24 bytes) and will be 8 bytes long
            var helloKey = transcoder.Converter.ToString(bytes, 24, 8);

            Assert.AreEqual(key, helloKey);
        }

        [Test]
        public void Key_Contains_CLR_And_OS()
        {
            const string defaultVersion = "0.0.0.0";
            var hello = new Hello(new short[] {}, new DefaultTranscoder(), 0, 0);

#if NET45
            var expectedKey = string.Format("couchbase-net-sdk/{0}, clr:{1}, os:{2}", defaultVersion, Environment.Version, Environment.OSVersion);
            Assert.AreEqual(expectedKey, hello.Key);
#else
            var expectedKey = string.Format("couchbase-net-sdk/{0}, clr:{1}, os:{2}", defaultVersion, RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription);
            Assert.AreEqual(expectedKey, hello.Key);
#endif
        }
    }
}
