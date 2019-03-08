namespace Couchbase.Core.IO.Operations.Legacy.Errors
{
    /// <summary>
    /// Thee type of retry strategy.
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// No retry strategy. This is the default value.
        /// </summary>
        None,

        /// <summary>
        /// The retry interval is a constant value.
        /// </summary>
        Constant,

        /// <summary>
        /// The retry interval grows in a linear fashion.
        /// </summary>
        Linear,

        /// <summary>
        /// The retry interval grows in an exponential fashion.
        /// </summary>
        Exponential
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
