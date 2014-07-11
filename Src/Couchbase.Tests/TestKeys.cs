using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Tests
{
    public static class TestKeys
    {
        static TestKeys()
        {
            KeyWithStringValue = new KeyValuePair<string, string>("Key_With_String_Value", "string value.");
            KeyWithInt32Value = new KeyValuePair<string, int>("Key_With_Int32_Value", 5242010);
        }
        public static KeyValuePair<string, string> KeyWithStringValue;
        public static KeyValuePair<string, int> KeyWithInt32Value;
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

#endregion