using System;
using System.Threading.Tasks;

namespace Couchbase.Core.Retry
{
    public struct ControlledBackoff : IBackoffCalculator
    {
        public Task Delay(IRequest request)
        {
            return Task.Delay(CalculateBackoff(request), request.Token);
        }

        public TimeSpan CalculateBackoff(IRequest request)
        {
            switch (request.Attempts)
            {
                case 0:
                    return TimeSpan.FromMilliseconds(1);
                case 1:
                    return TimeSpan.FromMilliseconds(10);
                case 2:
                    return TimeSpan.FromMilliseconds(50);
                case 3:
                    return TimeSpan.FromMilliseconds(100);
                case 4:
                    return TimeSpan.FromMilliseconds(500);
                default:
                    return TimeSpan.FromMilliseconds(1000);
            }
        }

        public static ControlledBackoff Create()
        {
            return new ControlledBackoff();
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
