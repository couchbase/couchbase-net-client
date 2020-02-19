using System;
using System.Threading;

namespace Couchbase.Query
{
    /// <summary>
    /// A decorator around a <see cref="Uri"/> that maintains count of the whether the last request failed.
    /// </summary>
    internal class FailureCountingUri : Uri
    {
        private int _failedCount;

        public FailureCountingUri([NotNull] string uriString) : base(uriString)
        {
        }

        /// <exception cref="UriFormatException">In the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, <see cref="T:System.FormatException" />, instead.<paramref name="uriString" /> contains a relative URI and <paramref name="uriKind" /> is <see cref="F:System.UriKind.Absolute" />.or<paramref name="uriString" /> contains an absolute URI and <paramref name="uriKind" /> is <see cref="F:System.UriKind.Relative" />.or<paramref name="uriString" /> is empty.-or- The scheme specified in <paramref name="uriString" /> is not correctly formed. See <see cref="M:System.Uri.CheckSchemeName(System.String)" />.-or- <paramref name="uriString" /> contains too many slashes.-or- The password specified in <paramref name="uriString" /> is not valid.-or- The host name specified in <paramref name="uriString" /> is not valid.-or- The file name specified in <paramref name="uriString" /> is not valid. -or- The user name specified in <paramref name="uriString" /> is not valid.-or- The host or authority name specified in <paramref name="uriString" /> cannot be terminated by backslashes.-or- The port number specified in <paramref name="uriString" /> is not valid or cannot be parsed.-or- The length of <paramref name="uriString" /> exceeds 65519 characters.-or- The length of the scheme specified in <paramref name="uriString" /> exceeds 1023 characters.-or- There is an invalid character sequence in <paramref name="uriString" />.-or- The MS-DOS path specified in <paramref name="uriString" /> must start with c:\\.</exception>
        /// <exception cref="ArgumentException"><paramref name="uriKind" /> is invalid. </exception>
        /// <exception cref="ArgumentNullException"><paramref name="uriString" /> is null. </exception>
        public FailureCountingUri([NotNull] string uriString, UriKind uriKind)
            : base(uriString, uriKind)
        {
        }

        /// <exception cref="ArgumentNullException"><paramref name="baseUri" /> is null. </exception>
        /// <exception cref="UriFormatException">In the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, <see cref="T:System.FormatException" />, instead.The URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is empty or contains only spaces.-or- The scheme specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> contains too many slashes.-or- The password specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The host name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The file name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid. -or- The user name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The host or authority name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> cannot be terminated by backslashes.-or- The port number specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid or cannot be parsed.-or- The length of the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> exceeds 65519 characters.-or- The length of the scheme specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> exceeds 1023 characters.-or- There is an invalid character sequence in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" />.-or- The MS-DOS path specified in <paramref name="baseUri" /> must start with c:\\.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseUri" /> is not an absolute <see cref="T:System.Uri" /> instance. </exception>
        public FailureCountingUri([NotNull] Uri baseUri, [NotNull] string relativeUri)
            : base(baseUri, relativeUri)
        {
        }

        /// <exception cref="ArgumentException"><paramref name="baseUri" /> is not an absolute <see cref="T:System.Uri" /> instance. </exception>
        /// <exception cref="ArgumentNullException"><paramref name="baseUri" /> is null. </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseUri" /> is not an absolute <see cref="T:System.Uri" /> instance. </exception>
        /// <exception cref="UriFormatException">In the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, <see cref="T:System.FormatException" />, instead.The URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is empty or contains only spaces.-or- The scheme specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> contains too many slashes.-or- The password specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The host name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The file name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid. -or- The user name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid.-or- The host or authority name specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> cannot be terminated by backslashes.-or- The port number specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> is not valid or cannot be parsed.-or- The length of the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> exceeds 65519 characters.-or- The length of the scheme specified in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" /> exceeds 1023 characters.-or- There is an invalid character sequence in the URI formed by combining <paramref name="baseUri" /> and <paramref name="relativeUri" />.-or- The MS-DOS path specified in <paramref name="baseUri" /> must start with c:\\.</exception>
        public FailureCountingUri([NotNull] Uri baseUri, [NotNull] Uri relativeUri)
            : base(baseUri, relativeUri)
        {
        }

        /// <summary>
        /// Increments the failed count by 1.
        /// </summary>
        public void IncrementFailed()
        {
            Interlocked.Increment(ref _failedCount);
        }

        /// <summary>
        /// Sets the failed count to zero indicating the <see cref="Uri"/> will execute requests successfully.
        /// </summary>
        public void ClearFailed()
        {
            Interlocked.Exchange(ref _failedCount, 0);
        }

        /// <summary>
        /// Gets the failed count.
        /// </summary>
        /// <value>
        /// The failed count.
        /// </value>
        public int FailedCount { get { return _failedCount; } }

        /// <summary>
        /// Determines whether the specified threshold is bueno.
        /// </summary>
        /// <param name="threshold">The threshold.</param>
        /// <returns></returns>
        public bool IsHealthy(int threshold)
        {
            return _failedCount < threshold;
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
