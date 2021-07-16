using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class UpsertUserOptions
    {
        internal string DomainNameValue { get; set; } = "local";
        internal CancellationToken TokenValue { get; set; }

        public UpsertUserOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public UpsertUserOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpsertUserOptions Default => new UpsertUserOptions();
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
