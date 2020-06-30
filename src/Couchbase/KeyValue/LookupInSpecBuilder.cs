using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.KeyValue
{
    public class LookupInSpecBuilder
    {
        internal readonly List<LookupInSpec> Specs = new List<LookupInSpec>();

        public LookupInSpecBuilder Get(string path, bool isXattr = false)
        {
            Specs.Add(LookupInSpec.Get(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder Exists(string path, bool isXattr = false)
        {
            Specs.Add(LookupInSpec.Exists(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder Count(string path, bool isXattr = false)
        {
            Specs.Add(LookupInSpec.Count(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder GetFull()
        {
            Specs.Add(LookupInSpec.GetFull());
            return this;
        }
    }
}
