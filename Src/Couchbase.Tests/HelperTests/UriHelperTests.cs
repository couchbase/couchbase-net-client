using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Helpers;

namespace Couchbase.Tests.HelperTests
{
    [TestFixture(Category="HelperTests")]
    public class UriHelperTests
    {
        [Test]
        public void When_Combining_Paths_With_Root_Uri_With_Trailing_Slash_No_Double_Slashes_Exist()
        {
            var rootUri = new Uri("http://localhost:8091/pools/");
            var combined = UriHelper.Combine(rootUri, "default");
            Assert.That(combined.AbsolutePath, Is.Not.StringContaining("//"));
        }

        [Test]
        public void When_Combining_Multiple_Paths_With_Leading_Slash_No_Double_Slashes_Exist()
        {
            var rootUri = new Uri("http://localhost:8091/pools/");
            var combined = UriHelper.Combine(rootUri, "/default", "/buckets");
            Assert.That(combined.AbsolutePath, Is.Not.StringContaining("//"));
            Assert.That(combined.AbsoluteUri, Is.StringMatching("http://localhost:8091/pools/default/buckets"));
        }

        [Test]
        public void When_Combining_Multiple_Paths_With_Trailing_Slash_No_Double_Slashes_Exist()
        {
            var rootUri = new Uri("http://localhost:8091/pools/");
            var combined = UriHelper.Combine(rootUri, "default", "buckets/");
            Assert.That(combined.AbsolutePath, Is.Not.StringContaining("//"));
            Assert.That(combined.AbsoluteUri, Is.StringMatching("http://localhost:8091/pools/default/buckets"));
        }

        [Test]
        public void When_Combining_Multiple_Paths_With_Trailing_And_Leading_Slash_No_Double_Slashes_Exist()
        {
            var rootUri = new Uri("http://localhost:8091/pools/");
            var combined = UriHelper.Combine(rootUri, "/default/", "/buckets/");
            Assert.That(combined.AbsolutePath, Is.Not.StringContaining("//"));
            Assert.That(combined.AbsoluteUri, Is.StringMatching("http://localhost:8091/pools/default/buckets"));
        }

        [Test]
        public void When_Combining_Multiple_Paths_With_Mixed_Trailing_And_Leading_Slash_No_Double_Slashes_Exist()
        {
            var rootUri = new Uri("http://localhost:8091/pools/");
            var combined = UriHelper.Combine(rootUri, "/default", "buckets/", "other");
            Assert.That(combined.AbsolutePath, Is.Not.StringContaining("//"));
            Assert.That(combined.AbsoluteUri, Is.StringMatching("http://localhost:8091/pools/default/buckets/other"));
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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