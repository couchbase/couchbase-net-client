using Couchbase.Core;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// Represents a search query request.
    /// </summary>
    public interface ISearchQuery
    {
        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        ISearchQuery Boost(double boost);

        /// <summary>
        /// Gets a JSON object representing this instance excluding any <see cref="ISearchParams"/>
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
}
