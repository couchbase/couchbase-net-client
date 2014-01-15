using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Extensions;
using NUnit.Framework;

namespace Couchbase
{
    [TestFixture]
    public class ArrayExtensionTests
    {
        [Test]
        public void TestShuffle()
        {
            var shuffled = new Uri[4];
            shuffled[0] = new Uri("http://192.168.56.101:8091/pools");
            shuffled[1] = new Uri("http://192.168.56.102:8091/pools");
            shuffled[2] = new Uri("http://192.168.56.103:8091/pools");
            shuffled[3] = new Uri("http://192.168.56.104:8091/pools");
            shuffled.Shuffle();

            var unshuffled = new Uri[4];
            unshuffled[0] = new Uri("http://192.168.56.101:8091/pools");
            unshuffled[1] = new Uri("http://192.168.56.102:8091/pools");
            unshuffled[2] = new Uri("http://192.168.56.103:8091/pools");
            unshuffled[3] = new Uri("http://192.168.56.104:8091/pools");

            var foundDifference = false;
            for (int i = 0; i < shuffled.Length; i++)
            {
                if (shuffled[i] != unshuffled[i])
                {
                    foundDifference = true;
                    break;
                }
            }
            Assert.IsTrue(foundDifference);
        }
    }
}
