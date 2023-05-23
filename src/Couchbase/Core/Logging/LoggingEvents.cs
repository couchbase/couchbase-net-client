namespace Couchbase.Core.Logging
{
    public static class LoggingEvents
    {
        public const int QueryEvent = 1000;

        public const int KvEvent = 2000;

        public const int ViewEvent = 3000;

        public const int SearchEvent = 4000;

        public const int AnalyticsEvent = 5000;

        public const int AuthenticationEvent = 6000;

        public const int ConfigEvent = 7000;

        public const int BootstrapEvent = 8000;

        public const int ThresholdEvent = 9000;

        public const int ChannelConnectionEvent = 10_000;
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
