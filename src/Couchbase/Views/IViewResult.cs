using System.Collections.Generic;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents the results of a View query.
    /// </summary>
    /// <typeparam name="T">The Type parameter to be used for deserialization by the <see cref="IDataMapper"/>
    /// implementation.</typeparam>
    public interface IViewResult
    {
        /// <summary>
        /// The results of the query if successful as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        IEnumerable<IViewRow> Rows { get; }

        /// <summary>
        /// Gets the query meta data.
        /// </summary>
        MetaData MetaData { get; }
    }

    public class MetaData
    {
        /// <summary>
        /// The total number of rows returned by the View request.
        /// </summary>
        public uint TotalRows { get; internal set; }
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
