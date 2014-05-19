using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class SortedDictionaryExtensionsTests
    {
        [Test]
        public void Test()
        {
            //1, 2, 8, 10, 10, 12, 19
            var dictionary = new SortedDictionary<string, long>();
            dictionary.Add(1.ToString(), 1);
            dictionary.Add(2.ToString(), 2);
            dictionary.Add(8.ToString(), 8);
            dictionary.Add(10.ToString(), 10);
            dictionary.Add(12.ToString(), 12);
            dictionary.Add(19.ToString(), 19);

            var index1 = dictionary.FindCeilingKey(1.ToString());
            Console.WriteLine(index1);
        }

        [Test]
        public void Test2()
        {
            var list = new List<int> {1, 2, 8, 10, 10, 12, 19};
            //1, 2, 8, 10, 10, 12, 19
            var dictionary = list.ToLookup(x => x.ToString());


            var index1 = dictionary.FindCeilingKey(1.ToString());
            Console.WriteLine(index1);

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