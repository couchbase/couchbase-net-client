using System;

namespace Couchbase.Views
{
    /// <summary>
    /// Allow the results from a stale view to be used
    /// </summary>
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    public enum StaleState
    {
        None,

        //Force a view update before returning data
        False,
        //Allow stale views
        Ok,
        //Allow stale view, update view after it has been accessed
        UpdateAfter
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
