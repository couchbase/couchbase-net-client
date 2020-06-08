using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Couchbase.IO;
using Microsoft.IO;
using NUnit.Framework;

namespace Couchbase.UnitTests.Serialization
{
    [TestFixture]
    public class DefaultSerializerTests
    {
        [Test]
        public void DefaultSerializer_Serializer_WorksWithRecyclableMemoryStream()
        {
            RecyclableMemoryStreamManager manager = new RecyclableMemoryStreamManager(2, 4, 4192 * 16);
            MemoryStreamFactory.SetFactoryFunc(() => manager.GetStream());
            var serializer = new DefaultSerializer();
            var output = serializer.Serialize(new {Value = "Foo"});
        }
    }
}
