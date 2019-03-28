using System;
using System.Collections.Generic;

namespace Couchbase.LoadTests.Helpers
{
    public class GuidKeyGenerator
    {
        private readonly string _prefix;

        public GuidKeyGenerator()
            : this("loadTest-")
        {
        }

        public GuidKeyGenerator(string prefix)
        {
            _prefix = prefix ?? "";
        }

        public IEnumerable<string> GenerateKeys(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return Guid.NewGuid().ToString();
            }
        }
    }
}
