using System.Reflection;
using Couchbase.IO.Http;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Http
{
    [TestFixture()]
    public class AuthenticatingHttpClientHandlerTests
    {
        [Test]
        public void Test_AllowPipelining_Is_False()
        {
#if NET45
            var handler = new AuthenticatingHttpClientHandler();
            Assert.IsFalse(handler.AllowPipelining);
#endif

        }

        [Test]
        public void Test_AllowPipelining_Is_False_When_Username_And_Password_Is_Set()
        {
#if NET45
            var handler = new AuthenticatingHttpClientHandler("username", "password");
            Assert.IsFalse(handler.AllowPipelining);
#endif
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
