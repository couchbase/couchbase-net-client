using System;
using Couchbase.IO;

namespace Couchbase
{
    public interface IDocumentFragment : IOperationResult
    {
        /// <summary>
        /// The value if it exists for a specific path.
        /// </summary>
        /// <typeparam name="TContent">The <see cref="Type"/> to cast the value to.</typeparam>
        /// <param name="path">The path of the operation to retrieve the value from.</param>
        /// <returns>An object of type <see cref="Type"/> representing the value of the operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        TContent Content<TContent>(string path);

        /// <summary>
        /// The value if it exists for a specific index.
        /// </summary>
        /// <typeparam name="TContent">The <see cref="Type"/> to cast the value to.</typeparam>
        /// <param name="index">The ordinal of the operation to retrieve the value from.</param>
        /// <returns>An object of type <see cref="Type"/> representing the value of the operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        TContent Content<TContent>(int index);

        /// <summary>
        /// The value if it exists for a specific path.
        /// </summary>
        /// <param name="path">The path of the operation to retrieve the value from.</param>
        /// <returns>An <see cref="object"/> representing the result of a operation.</returns>
        /// <remarks>If no value exists, the default value (null) for the <see cref="object"/> will be returned.</remarks>
        object Content(string path);

        /// <summary>
        /// The value if it exists for a specific index.
        /// </summary>
        /// <param name="index">The ordinal of the operation to retrieve the value from.</param>
        /// <returns>An <see cref="object"/> representing the result of a operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        /// <remarks>If no value exists, the default value (null) for the <see cref="object"/> will be returned.</remarks>
        object Content(int index);

        /// <summary>
        /// Checks whether the given path is part of this result set, eg. an operation targeted it, and the operation executed successfully.
        /// </summary>
        /// <param name="path">The path for the sub-document operation.</param>
        /// <returns><s>true</s> if that path is part of the successful result set, <s>false</s> in any other case.</returns>
        bool Exists(string path);

        /// <summary>
        /// The count of the sub-document operations chained togather.
        /// </summary>
        /// <returns>An <see cref="int"/> that is the count of the total operations chained togather.</returns>
        int Count();

        /// <summary>
        /// Gets the <see cref="ResponseStatus"/> for a specific operation at it's path.
        /// </summary>
        /// <param name="path">The path of the operation.</param>
        /// <returns>The <see cref="ResponseStatus"/> that the server returned.</returns>
        ResponseStatus OpStatus(string path);

        /// <summary>
        /// Gets the <see cref="ResponseStatus"/> for a specific operation at it's index.
        /// </summary>
        /// <param name="index">The ordinal of the operation.</param>
        /// <returns>The <see cref="ResponseStatus"/> that the server returned.</returns>
        ResponseStatus OpStatus(int index);
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
