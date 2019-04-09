using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// An abstract class for creating <see cref="ISearchFacet"/> implementations.
    /// </summary>
    public abstract class SearchFacet : ISearchFacet
    {
        /// <summary>
        /// The name of the facet.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The field of the facet.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The number of facets or categories returned.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets the JSON representation of this object.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Name and the Field property must have a value.</exception>
        /// <returns>A <see cref="JObject"/> representing the object's state.</returns>
        public virtual JProperty ToJson()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("The Name property must have a value.");
            }
            if (string.IsNullOrWhiteSpace(Field))
            {
                throw new InvalidOperationException("The Field property must have a value.");
            }
            return new JProperty(Name, new JObject(
                    new JProperty("field", Field),
                    new JProperty("size", Size)));
        }

        /// <summary>
        /// Factory for creating <see cref="TermFacet"/> instances.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        public static TermFacet Term(string name, string field, int size)
        {
            return new TermFacet();
        }

        /// <summary>
        /// Factory for creating <see cref="NumericRangeFacet"/> instances.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <param name="ranges">The ranges.</param>
        /// <returns></returns>
        public static NumericRangeFacet Numeric(string name, string field, int size, params Range<float>[] ranges)
        {
            return new NumericRangeFacet
            {
                Name = name,
                Field = field,
                Size = size
            }.AddRanges(ranges);
        }

        /// <summary>
        /// Factory for creating <see cref="DateRangeFacet"/> instances.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <param name="ranges">The ranges.</param>
        /// <returns></returns>
        public static DateRangeFacet Date(string name, string field, int size, params Range<DateTime>[] ranges)
        {
            return new DateRangeFacet
            {
                Name = name,
                Field = field,
                Size = size
            }.AddRanges(ranges);
        }

        public override string ToString()
        {
            return ToJson().ToString();
        }
    }

    #region [ License information ]

    /* ************************************************************
     *
     *    @author Couchbase <info@couchbase.com>
     *    @copyright 2014 Couchbase, Inc.
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

    #endregion
}
