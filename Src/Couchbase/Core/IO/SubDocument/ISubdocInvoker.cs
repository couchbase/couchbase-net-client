using System;
using System.Threading.Tasks;

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
        /// Invokes the chained operations on the <see cref="IMutateInBuilder{TDocument}"/> instance.
        /// </summary>
        /// <typeparam name="T">The document's <see cref="Type"/> for building paths.</typeparam>
        /// <param name="builder">The <see cref="IMutateInBuilder{TDocument}"/> that contains a list of chained mutate operations.</param>
        /// <returns>A <see cref="IDocumentFragment{TDocument}"/> with the results for each mutate operation.</returns>
        Task<IDocumentFragment<T>> InvokeAsync<T>(IMutateInBuilder<T> builder);

        /// <summary>
        /// Invokes the chained operations on the <see cref="ILookupInBuilder{TDocument}"/> instance.
        /// </summary>
        /// <typeparam name="T">The document's <see cref="Type"/> for building paths.</typeparam>
        /// <param name="builder">The <see cref="ILookupInBuilder{TDocument}"/> that contains a list of chained lookup operations.</param>
        /// <returns>A <see cref="IDocumentFragment{TDocument}"/> with the results for each lookup operation.</returns>
        IDocumentFragment<T> Invoke<T>(ILookupInBuilder<T> builder);

        /// <summary>
        /// Invokes the chained operations on the <see cref="ILookupInBuilder{TDocument}"/> instance.
        /// </summary>
        /// <typeparam name="T">The document's <see cref="Type"/> for building paths.</typeparam>
        /// <param name="builder">The <see cref="ILookupInBuilder{TDocument}"/> that contains a list of chained lookup operations.</param>
        /// <returns>A <see cref="IDocumentFragment{TDocument}"/> with the results for each lookup operation.</returns>
        Task<IDocumentFragment<T>> InvokeAsync<T>(ILookupInBuilder<T> builder);
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
