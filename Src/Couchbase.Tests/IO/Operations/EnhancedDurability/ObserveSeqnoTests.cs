using System.Collections.Generic;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.EnhancedDurability;
using Couchbase.IO.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations.EnhancedDurability
{
    [TestFixture]
    public class ObserveSeqnoTests : OperationTestBase
    {
        [Test]
        public void Test_ObserveSeqno_Parse_Packet()
        {
            var converter = new DefaultConverter();
            var packet = new byte []
            {
                0x81, //magic
                0x01, //opcode
                0x00, 0x00, //key length
                0x10, //extra length
                0x00, //data type
                0x00, 0x00, //status
                0x00, 0x00, 0x00, 0x10, //total body
                0x00, 0x00, 0x00, 0x01, //opaque
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,//cas
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x39, //uuid
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2D //seqno
            };
            var extras = new byte[converter.ToByte(packet, HeaderIndexFor.ExtrasLength)];
            var uuid = converter.ToUInt64(packet, 24);
            var seqno = converter.ToInt64(packet, 32);

            Assert.AreEqual(12345, uuid);
            Assert.AreEqual(45, seqno);
        }

        [Test]
        public void Test_ObserveSeqno2()
        {
            var key = "bar";
            var delete = new Delete(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var deleteResult = IOService.Execute(delete);

            var features = new List<short>();
            features.Add((short)ServerFeatures.MutationSeqno);

            var hello = new Hello("couchbase-net-sdk/2.1.4", features.ToArray(), Transcoder, 0, 0);
            var result = IOService.Execute(hello);
            Assert.IsTrue(result.Success);

            var add = new Add<string>(key, "foo", GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result2 = IOService.Execute(add);

            var observeSeqno = new ObserveSeqno(result2.Token, Transcoder, 1000);
            var observeSeqnoResult = IOService.Execute(observeSeqno);
            Assert.IsTrue(observeSeqnoResult.Success);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
