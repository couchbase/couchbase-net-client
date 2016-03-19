using System;

namespace Couchbase.Core.IO.SubDocument
{
    /// <summary>
    /// Works as a shim between <see cref="IBucket"/> and <see cref="IMutateInBuilder{TDocument}"/> and <see cref="ILookupInBuilder{TDocument}"/> invoking a request for the chained operations.
    /// </summary>
    internal interface ISubdocInvoker
    {
        /// <summary>
        /// Invokes the chained operations on the <see cref="IMutateInBuilder{TDocument}"/> instance.
        /// </summary>
        /// <typeparam name="T">The document's <see cref="Type"/> for building paths.</typeparam>
        /// <param name="builder">The <see cref="IMutateInBuilder{TDocument}"/> that contains a list of chained mutate operations.</param>
        /// <returns>A <see cref="IDocumentFragment{TDocument}"/> with the results for each mutate operation.</returns>
        IDocumentFragment<T> Invoke<T>(IMutateInBuilder<T> builder);

        /// <summary>
        /// Invokes the chained operations on the <see cref="ILookupInBuilder{TDocument}"/> instance.
        /// </summary>
        /// <typeparam name="T">The document's <see cref="Type"/> for building paths.</typeparam>
        /// <param name="builder">The <see cref="ILookupInBuilder{TDocument}"/> that contains a list of chained lookup operations.</param>
        /// <returns>A <see cref="IDocumentFragment{TDocument}"/> with the results for each lookup operation.</returns>
        IDocumentFragment<T> Invoke<T>(ILookupInBuilder<T> builder);
    }
}
