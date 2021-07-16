using System.Collections.Generic;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInSpecBuilder
    {
        internal readonly List<MutateInSpec> Specs = new List<MutateInSpec>();

        public MutateInSpecBuilder Insert<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Insert(path, value, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder Upsert<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Upsert(path, value, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder Replace<T>(string path, T value, bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Replace(path, value, isXattr));
            return this;
        }

        public MutateInSpecBuilder SetDoc<T>(T value)
        {
            Specs.Add(MutateInSpec.SetDoc(value));
            return this;
        }

        public MutateInSpecBuilder Remove(string path, bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Remove(path, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayAppend<T>(string path, T[] values, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAppend(path, values, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayAppend<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAppend(path, value, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayPrepend<T>(string path, T[] values, bool createParents = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayPrepend(path, values, createParents, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayPrepend<T>(string path, T value, bool createParents = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayPrepend(path, value, createParents, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayInsert<T>(string path, T[] values, bool createParents= default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayInsert(path, values, createParents, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayInsert<T>(string path, T value, bool createParents= default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayInsert(path, value, createParents, isXattr));
            return this;
        }

        public MutateInSpecBuilder ArrayAddUnique<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAddUnique(path, value, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder Increment(string path, long delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Increment(path, delta, createPath, isXattr));
            return this;
        }

        public MutateInSpecBuilder Decrement(string path, long delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            // delta must be negative
            if (delta > 0)
            {
                delta = -delta;
            }

            Specs.Add(MutateInSpec.Decrement(path, delta, createPath, isXattr));
            return this;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
