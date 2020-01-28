using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries
{
    /// <summary>
    /// Base class for <see cref="ISearchQuery"/> implementations.
    /// </summary>
    /// <seealso cref="ISearchQuery" />
    public abstract class SearchQueryBase : ISearchQuery
    {
        private const double DefaultBoostValue = 1.0;

        private double _boost = DefaultBoostValue;
        protected string IndexName;
        protected string Query;

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public ISearchQuery Boost(double boost)
        {
            if (boost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boost), "Must be greater than or equal to zero.");
            }
            _boost = boost;
            return this;
        }

        /// <summary>
        /// Gets a JSON object representing this query instance />
        /// </summary>
        /// <returns></returns>
        public virtual JObject Export()
        {
            var json = new JObject();
            if (!_boost.Equals(DefaultBoostValue))
            {
                json.Add(new JProperty("boost", _boost));
            }

            return json;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
