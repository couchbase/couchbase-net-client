using System.ComponentModel;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// Specifies how the individual match terms should be logically concatenated.
    /// </summary>
    public enum MatchOperator
    {
        /// <summary>
        /// Specifies that individual match terms are concatenated with a logical OR - this is the default if not provided.
        /// </summary>
        [Description("or")]
        Or,

        /// <summary>
        /// Specifies that individual match terms are concatenated with a logical AND.
        /// </summary>
        [Description("and")]
        And
    }
}
