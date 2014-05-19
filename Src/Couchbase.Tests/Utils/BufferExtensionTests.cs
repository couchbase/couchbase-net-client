using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class BufferExtensionTests
    {
        [Test]
        public void When_Array_Is_Null_GetLengthSafe_Returns_Zero()
        {
            byte[] buffer = null;
            const int expected = 0;
      
            var actual = buffer.GetLengthSafe();
            Assert.AreEqual(expected, actual);
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