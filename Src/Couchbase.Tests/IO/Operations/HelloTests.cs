using System.Collections.Generic;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.EnhancedDurability;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class HelloTests : OperationTestBase
    {
        [Test]
        public void Test_Hello_With_Feature_MutationSeqno_Set()
        {
            var features = new List<short>();
            features.Add((short)ServerFeatures.MutationSeqno);

            var hello = new Hello("couchbase-net-sdk/2.1.4", features.ToArray(), Transcoder, 0, 0);
            var result = IOStrategy.Execute(hello);
            Assert.IsTrue(result.Success);
            var key = "bar";

            var delete = new Delete(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var deleteResult = IOStrategy.Execute(delete);

            var add = new Add<string>(key, "foo", GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result2 =IOStrategy.Execute(add);
            Assert.IsNotNull(result2.Token);
        }

        [Test]
        public void When_MutationSeqno_Is_Not_Set_MutationToken_Is_The_Same_For_All_Instances()
        {
            var key = "bar";

            var delete = new Delete(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var deleteResult = IOStrategy.Execute(delete);

            var add = new Add<string>(key, "foo", GetVBucket(), Transcoder, OperationLifespanTimeout);
            var addResult = IOStrategy.Execute(add);
            Assert.IsNotNull(addResult.Token);
            Assert.AreEqual(deleteResult.Token, addResult.Token);
        }

        [Test]
        public void Test_Hello_With_Features_MutationSeqno_And_TcpNodelay_Set()
        {
            var features = new List<short>();
            features.Add((short)ServerFeatures.MutationSeqno);
            features.Add((short)ServerFeatures.TcpNoDelay);

            var hello = new Hello("couchbase-net-sdk/2.1.4", features.ToArray(), Transcoder, 0, 0);
            var result = IOStrategy.Execute(hello);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Hello_No_Features_Set()
        {
            var features = new List<short>();

            var hello = new Hello("couchbase-net-sdk/2.1.4", features.ToArray(), Transcoder, 0, 0);
            var result = IOStrategy.Execute(hello);
            Assert.IsTrue(result.Success);
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
