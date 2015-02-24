namespace Couchbase.N1QL
{
    public class QueryPlan : IQueryPlan
    {
        private readonly string _plan;

        public QueryPlan(string plan)
        {
            _plan = plan;
        }

        /// <summary>
        /// Gives the N1QL string representation of the query plan.
        /// </summary>
        /// <returns>
        /// The N1QL string for the query plan.
        /// </returns>
        public string ToN1ql()
        {
            return _plan;
        }
    }
}