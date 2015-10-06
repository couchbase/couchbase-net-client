using System;
using System.Threading;
using Couchbase.Core;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class LifespanTests
    {
        [Test]
        public void When_Duration_NotExceeded_Timeout_Returns_False()
        {
            var lifespan = new Lifespan
            {
                CreationTime = DateTime.UtcNow,
                Duration = 500
            };

            Thread.Sleep(250);
            Assert.IsFalse(lifespan.TimedOut());
        }

        [Test]
        public void When_Duration_Exceeded_Timeout_Returns_True()
        {
            var lifespan = new Lifespan
            {
                CreationTime = DateTime.UtcNow,
                Duration = 250
            };

            Thread.Sleep(500);
            Assert.IsTrue(lifespan.TimedOut());
        }

        [Test]
        public void When_Duration_Exceeded_Timeout_Returns_True_Multiple_Tries()
        {
            var lifespan = new Lifespan
            {
                CreationTime = DateTime.UtcNow,
                Duration = 500
            };

            Thread.Sleep(100);
            Assert.IsFalse(lifespan.TimedOut());
            Thread.Sleep(600);
            Assert.IsTrue(lifespan.TimedOut());
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
