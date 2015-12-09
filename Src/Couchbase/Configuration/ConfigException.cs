using System;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Generic exception thrown when a configuration cannot be bootstrapped or is the wrong type for the given bucket.
    /// </summary>
    public class ConfigException : Exception
    {
        public ConfigException()
        {
        }

        public ConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ConfigException(string message) : base(message)
        {
        }

        public ConfigException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
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