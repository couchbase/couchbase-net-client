using System;

namespace Couchbase.Core
{
    /// <summary>
    /// This exception is thrown when the given operation targeting a specific replica is not fulfillable because the
    /// replica is not configured (for example replica 2 is asked for, but only 1 is configured).
    /// </summary>
    public class ReplicaNotConfiguredException : Exception
    {
        public ReplicaNotConfiguredException(string message)
            : base(message)
        {
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
