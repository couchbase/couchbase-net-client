using System;
using System.Collections.Generic;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase.Services.KeyValue
{
    public class LookupInSpecBuilder
    {
        internal readonly List<OperationSpec> Specs = new List<OperationSpec>();

        public LookupInSpecBuilder Get(string path, bool isXattr = false)
        {
            Validate("Get", path);
            Specs.Add(LookupInSpec.Get(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder Exists(string path, bool isXattr = false)
        {
            Validate("Exists", path);
            Specs.Add(LookupInSpec.Exists(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder Count(string path, bool isXattr = false)
        {
            Validate("Count", path);
            Specs.Add(LookupInSpec.Count(path, isXattr));
            return this;
        }

        public LookupInSpecBuilder GetFull()
        {
            Validate("GetFull", "");
            Specs.Add(LookupInSpec.GetFull());
            return this;
        }

        private void Validate(string op, string path)
        {
            if (Specs.Exists(x => x.PathFlags == SubdocPathFlags.Xattr))
            {
                throw new ArgumentException($"Only a single XAttr key may be accessed at the same time: {op}(\"{path}\", isXattr: true);");
            }
        }
    }
}
