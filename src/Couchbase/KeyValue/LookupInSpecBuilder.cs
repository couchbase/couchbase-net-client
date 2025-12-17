using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// A builder for chaining together lookup specs into a JSON document.
    /// </summary>
    public class LookupInSpecBuilder
    {
        internal readonly List<LookupInSpec> Specs = [];

        internal LookupInSpecBuilder Get(string path, bool isXattr, bool isBinary)
        {
            Specs.Add(LookupInSpec.Get(path, isXattr, isBinary));
            return this;
        }

        /// <summary>
        /// Fetches the value of an attribute for a given path.
        /// </summary>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="LookupInSpecBuilder"/> for chaining specs.</returns>
        public LookupInSpecBuilder Get(string path, bool isXattr = false)
        {
            Get(path, isXattr: isXattr, isBinary: false);
            return this;
        }

        /// <summary>
        /// Checks for the existence of a value given a path.
        /// </summary>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="LookupInSpecBuilder"/> for chaining specs.</returns>
        public LookupInSpecBuilder Exists(string path, bool isXattr = false)
        {
            Specs.Add(LookupInSpec.Exists(path, isXattr));
            return this;
        }

        /// <summary>
        /// Provides a count of a dictionary or list attribute given a JSON path.
        /// </summary>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="LookupInSpecBuilder"/> for chaining specs.</returns>
        public LookupInSpecBuilder Count(string path, bool isXattr = false)
        {
            Specs.Add(LookupInSpec.Count(path, isXattr));
            return this;
        }

        /// <summary>
        /// Fetches the entire JSON document for a key.
        /// </summary>
        /// <returns>A <see cref="LookupInSpecBuilder"/> for chaining specs.</returns>
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
