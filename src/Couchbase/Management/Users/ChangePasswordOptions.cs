using System.Threading;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations.Errors;

#nullable enable

namespace Couchbase.Management.Users
{
    public class ChangePasswordOptions
    {
        internal string DomainNameValue { get; set; } = "/controller/changePassword";
        internal CancellationToken TokenValue { get; set; }

        internal RequestSpan? ParentSpan { get; set; }


        public ChangePasswordOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public ChangePasswordOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static ChangePasswordOptions Default => new ChangePasswordOptions();
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
