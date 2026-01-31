using System;
using System.ComponentModel;

namespace Couchbase.Views
{
   /// <summary>
    /// Sets the desired index scan consistency for current N1QL query.
    /// </summary>
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    public enum ViewScanConsistency
    {
        /// <summary>
        /// This value specifies that the view engine must update the index before executing the view query.
        /// </summary>
        [Description("false")]
        RequestPlus,

        /// <summary>
        /// The views engine uses the existing index as the basis of the query and marks the index to
        /// be updated after the results are returned to the client.
        /// </summary>
        [Description("update_after")]
        UpdateAfter,

        /// <summary>
        ///This value specifies that the view engine can use the existing index "as is" and does not
        ///need to update the index
        /// </summary>
        [Description("ok")]
        NotBounded,
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
