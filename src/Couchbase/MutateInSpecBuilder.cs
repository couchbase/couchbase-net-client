using System.Collections.Generic;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public class MutateInSpecBuilder
    {
        internal readonly List<OperationSpec> Specs = new List<OperationSpec>();

        public MutateInSpecBuilder Insert<T>(string path, T value, bool createPath = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.Insert(path, value, createPath, xAttr));
            return this;
        }

        public MutateInSpecBuilder Upsert<T>(string path, T value, bool createPath = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.Upsert(path, value, createPath, xAttr));
            return this;
        }

        public MutateInSpecBuilder Replace<T>(string path, T value, bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.Replace(path, value, xAttr));
            return this;
        }

        public MutateInSpecBuilder Remove(string path, bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.Remove(path, xAttr));
            return this;
        }

        public MutateInSpecBuilder ArrayAppend<T>(string path, T[] values, bool createPath = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAppend(path, values, createPath, xAttr));
            return this;
        }

        public MutateInSpecBuilder ArrayPrepend<T>(string path, T[] values, bool createParents = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayPrepend(path, values, createParents, xAttr));
            return this;
        }

        public MutateInSpecBuilder ArrayInsert<T>(string path, T[] values, bool createParents= default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayInsert(path, values, createParents, xAttr));
            return this;
        }

        public MutateInSpecBuilder ArrayAddUnique<T>(string path, T value, bool createPath = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAddUnique(path, value, createPath, xAttr));
            return this;
        }

        public MutateInSpecBuilder Increment(string path, long delta, bool createPath = default(bool), bool xAttr = default(bool))
        {
            Specs.Add(MutateInSpec.Increment(path, delta, createPath, xAttr));
            return this;
        }

        public MutateInSpecBuilder Decrement(string path, long delta, bool createPath = default(bool), bool xAttr = default(bool))
        {
            // delta must be negative
            if (delta > 0)
            {
                delta = -delta;
            }

            Specs.Add(MutateInSpec.Decrement(path, delta, createPath, xAttr));
            return this;
        }
    }
}
