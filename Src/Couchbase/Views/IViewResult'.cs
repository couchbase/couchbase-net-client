using System.Collections.Generic;
using System.Net;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents the results of a View query.
    /// </summary>
    /// <typeparam name="T">The Type parameter to be used for deserialization by the <see cref="IDataMapper"/>
    /// implementation.</typeparam>
    public interface IViewResult<T> : IResult
    {
        /// <summary>
        /// The total number of rows returned by the View request.
        /// </summary>
        uint TotalRows { get; }

        /// <summary>
        /// The results of the query if successful as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        IEnumerable<ViewRow<T>> Rows { get; }

        /// <summary>
        /// A View engine specific error message if one occured.
        /// </summary>
        string Error { get; }

        /// <summary>
        /// The HTTP Status Code for the request
        /// </summary>
        HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Returns the value of each element within the <see cref="Rows"/> property as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        IEnumerable<T> Values { get; }

        /// <summary>
        /// Returns false if the error that caused the View request to fail can result in a retry request.
        /// </summary>
        /// <returns></returns>
        bool CannotRetry();
    }
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
