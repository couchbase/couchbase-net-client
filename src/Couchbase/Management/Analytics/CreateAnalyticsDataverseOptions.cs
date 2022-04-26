using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDataverseOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }

        public CreateAnalyticsDataverseOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public CreateAnalyticsDataverseOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan? TimeoutValue { get; set; }

        public CreateAnalyticsDataverseOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        internal AnalyticsOptions CreateAnalyticsOptions()
        {
            var options = new AnalyticsOptions()
                .CancellationToken(TokenValue);

            if (TimeoutValue.HasValue)
            {
                  options.Timeout(TimeoutValue.Value);
            }

            return options;
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
