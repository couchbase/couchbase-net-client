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
