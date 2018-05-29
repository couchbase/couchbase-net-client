using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Moq;

namespace Couchbase.UnitTests.Collections
{
    public static class MockHelper
    {
        public static Mock<IBucket> CreateBucket<T>(string documentKey, params T[] items)
        {
            var result = new Mock<IOperationResult<List<T>>>();
            result.SetupGet(x => x.Success).Returns(true);
            result.SetupGet(x => x.Value).Returns(items.ToList());

            var bucket = new Mock<IBucket>();
            bucket.Setup(x => x.Exists(documentKey)).Returns(true);
            bucket.Setup(x => x.Get<List<T>>(documentKey)).Returns(result.Object);

            return bucket;
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
