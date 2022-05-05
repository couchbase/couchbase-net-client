namespace Couchbase
{
    /// <summary>
    /// Extension methods with more convenient overloads for ServiceResult.
    /// </summary>
    public static class ServiceResultExtensions
    {
        /// <summary>
        /// Throw exception in the "No Retry" scenario
        /// </summary>
        /// <param name="result">The Service Result</param>
        public static void ThrowOnNoRetry(this IServiceResult result)
        {
            if (result is IServiceResultExceptionInfo resultWithException)
            {
                if (resultWithException.NoRetryException != null)
                {
                    throw resultWithException.NoRetryException;
                }
                else
                {
                    throw new CouchbaseException();
                }
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2022 Couchbase, Inc.
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
