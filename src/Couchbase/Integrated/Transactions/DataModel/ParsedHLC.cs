#if NET5_0_OR_GREATER
#nullable enable
using System;
using Newtonsoft.Json;

namespace Couchbase.Integrated.Transactions.DataModel
{
    internal record ParsedHLC(string now, string mode)
    {
        [JsonIgnore]
        public DateTimeOffset NowTime => DateTimeOffset.FromUnixTimeSeconds(int.Parse(now));

        [JsonIgnore]
        public CasMode CasModeParsed => mode switch {
                "l" => CasMode.LOGICAL,
                "logical" => CasMode.LOGICAL,
                "r" => CasMode.REAL,
                "real" => CasMode.REAL,
                _ => CasMode.UNKNOWN
            };

        internal enum CasMode
        {
            UNKNOWN = 0,
            REAL = 'r',
            LOGICAL = 'l'
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
#endif
