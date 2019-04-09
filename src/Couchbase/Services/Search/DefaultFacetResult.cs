namespace Couchbase.Services.Search
{
    /// <summary>
    /// The default facet result.
    /// </summary>
    public class DefaultFacetResult : IFacetResult
    {
        /// <summary>
        /// Gets or sets the name of the result.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets the field used for the facet.
        /// </summary>
        /// <value>
        /// The field.
        /// </value>
        public string Field { get; set; }

        /// <summary>
        /// Gets the total number of hits for the facet.
        /// </summary>
        /// <value>
        /// The total.
        /// </value>
        public long Total { get; set; }

        /// <summary>
        /// Gets the number of misses for the facet.
        /// </summary>
        /// <value>
        /// The missing.
        /// </value>
        public long Missing { get; set; }

        /// <summary>
        /// Gets the number of others for the facet.
        /// </summary>
        /// <value>
        /// The other.
        /// </value>
        public long Other { get; set; }

        /// <summary>
        /// Gets the type of the facet result.
        /// </summary>
        /// <value>
        /// The type of the facet result.
        /// </value>
        public virtual FacetResultType FacetResultType { get { return FacetResultType.Unknown;} }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
