using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Sort
{
    public abstract class SearchSortBase : ISearchSort
    {
        protected abstract string By { get; }

        protected bool Decending { get; set; }

        /// <summary>
        /// Gets a JSON object representing this search sort.
        /// </summary>
        public virtual JObject Export()
        {
            var json = new JObject
            {
                {"by", By}
            };

            if (Decending)
            {
                json.Add(new JProperty("decending", true));
            }

            return json;
        }
    }
}