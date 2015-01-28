﻿using System;

namespace Couchbase.IO.Operations
{
    public struct OperationHeader
    {
        private const int HeaderLength = 24;

        public int Magic { get; set; }

        public OperationCode OperationCode { get; set; }

        public string Key { get; set; }

        public int ExtrasLength { get; set; }

        public TypeCode DataType { get; set; }

        public ResponseStatus Status { get; set; }

        public int KeyLength { get; set; }

        public int BodyLength { get; set; }

        public uint Opaque { get; set; }

        public ulong Cas { get; set; }

        public bool HasData()
        {
            return BodyLength > 0;
        }

        public int TotalLength { get { return BodyLength + HeaderLength; } }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information          ]