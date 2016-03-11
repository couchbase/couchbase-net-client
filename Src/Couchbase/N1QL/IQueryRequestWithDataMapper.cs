using Couchbase.Views;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Extends <see cref="IQueryRequest"/> to provide a custom data mapper
    /// </summary>
    interface IQueryRequestWithDataMapper : IQueryRequest
    {
        /// <summary>
        /// Custom <see cref="IDataMapper"/> to use when deserializing query results.
        /// </summary>
        /// <remarks>Null will use the default <see cref="IDataMapper"/>.</remarks>
        IDataMapper DataMapper { get; set; }
    }
}
