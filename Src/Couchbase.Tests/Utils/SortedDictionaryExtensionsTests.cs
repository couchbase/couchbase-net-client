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
