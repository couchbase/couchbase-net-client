using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim;
using Enyim.Caching.Memcached.Results;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class StatusCodeExtensionTests
    {
        [Test]
        public void Test_ToInt()
        {
            var result = new GetOperationResult();
            result.StatusCode = 2;

            Assert.AreEqual(StatusCode.KeyExists.ToInt(), result.StatusCode);
        }

        [Test]
        public void Test_ToEnum()
        {
            var result = new GetOperationResult();
            result.StatusCode = 2;

            Assert.AreEqual(StatusCode.KeyExists, result.StatusCode.ToEnum());
        }
    }
}