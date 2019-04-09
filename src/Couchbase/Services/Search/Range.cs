namespace Couchbase.Services.Search
{
    /// <summary>
    /// Represents a range of values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class Range<T>
    {
        /// <summary>
        /// Gets or sets the name for the range.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the start value.
        /// </summary>
        /// <value>
        /// The start.
        /// </value>
        public T Start { get; set; }

        /// <summary>
        /// Gets or sets the end value.
        /// </summary>
        /// <value>
        /// The end.
        /// </value>
        public T End { get; set; }
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
}
