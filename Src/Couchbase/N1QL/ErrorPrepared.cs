namespace Couchbase
{
    /// <summary>
    /// An enum listing the N1QL codes that should trigger a retry for non adhoc queries.
    /// </summary>
    /// <remarks>Generic (5000) also needs a check of the message content to determine if
    /// retry is applicable or not</remarks>
    internal enum ErrorPrepared
    {
        Unrecognized = 4050,
        UnableToDecode = 4070,
        Generic = 5000,
        IndexNotFound = 12016
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
