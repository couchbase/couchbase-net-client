using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry;
using Couchbase.Query;

#nullable enable

namespace Couchbase.Analytics
{
    internal abstract class AnalyticsResultBase<T> : IAnalyticsResult<T>, IServiceResultExceptionInfo
    {
        /// <summary>
        /// Creates a new AnalyticsResultBase.
        /// </summary>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        protected AnalyticsResultBase(Stream responseStream)
        {
            ResponseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        }

        /// <inheritdoc />
        public IAsyncEnumerable<T> Rows => this;

        /// <summary>
        /// Response stream being deserialized.
        /// </summary>
        protected Stream ResponseStream { get; }

        /// <inheritdoc />
        public AnalyticsMetaData? MetaData { get; set; }

        public List<Error> Errors { get; set; } = new List<Error>();
        public HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        public bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return RetryReason != RetryReason.NoRetry;
        }

        private void SetRetryReasonIfFailed()
        {
            if (HttpStatusCode == HttpStatusCode.OK)
                RetryReason = RetryReason.NoRetry;
            else
            {
                foreach (var error in Errors)
                {
                    switch (error.Code)
                    {
                        case 21002:
                            throw new AmbiguousTimeoutException("Analytics query timed out.");
                        case 23000:
                        case 23003:
                        case 23007:
                            RetryReason = RetryReason.AnalyticsTemporaryFailure;
                            return;
                    }
                }
            }
        }

        public RetryReason RetryReason { get; protected set; } = RetryReason.NoRetry;

        public Exception? NoRetryException { get; set; }

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task.</returns>
        public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual void Dispose()
        {
            ResponseStream?.Dispose();
        }
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
