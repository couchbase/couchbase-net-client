using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class DropQueryIndexOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public DropQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public DropQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropQueryIndexOptions Default => new DropQueryIndexOptions();
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
