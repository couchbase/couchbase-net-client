using System.Collections.Generic;

namespace Couchbase.Analytics
{
    public interface IAnalyticsResult<T>
    {
        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        List<T> Rows { get; }

        /// <summary>
        /// Gets the meta data associated with the analytics result.
        /// </summary>
        MetaData MetaData { get; }

        /// <summary>
        /// Gets the deferred query handle if requested.
        /// <para>
        /// The handle can be used to retrieve a deferred query status and results.
        /// </para>
        /// </summary>
        IAnalyticsDeferredResultHandle<T> Handle { get; }
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
