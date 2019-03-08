using System.Collections.Generic;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public class LookupInSpecBuilder
    {
        internal readonly List<OperationSpec> Specs = new List<OperationSpec>();

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
    }
}
