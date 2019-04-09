using Couchbase.Core;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// Represents a search query request.
    /// </summary>
    public interface IFtsQuery
    {
        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        IFtsQuery Boost(double boost);

        /// <summary>
        /// Gets a JSON object representing this instance excluding any <see cref="ISearchOptions"/>
        /// </summary>
        /// <returns></returns>
        JObject Export();

        /// <summary>
        /// Sets the lifespan of the search request; used to check if the request exceeded the maximum time
        /// configured for it in <see cref="ClientConfiguration.SearchRequestTimeout"/>
        /// </summary>
        /// <value>
        /// The lifespan.
        /// </value>
        Lifespan Lifespan { get; set; }

        /// <summary>
        /// True if the request exceeded it's <see cref="ClientConfiguration.SearchRequestTimeout"/>
        /// </summary>
        /// <returns><c>true</c> if the request has timed out.</returns>
        bool TimedOut();
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
