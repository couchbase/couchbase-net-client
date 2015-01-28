﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Tests.Mocks
{
    public class MockHttpClient : IHttpClient
    {
        public Uri BaseUri { get; set; }

        public IHttpRequest Request
        {
            get
            {
                return _request;
            }
            set
            {
                _request = value;
            }
        }

        private IHttpRequest _request;

        public IHttpRequest CreateRequest(string path)
        {
            return (Request = new MockHttpRequest() { Path = path });
        }

        public int RetryCount { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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

#endregion