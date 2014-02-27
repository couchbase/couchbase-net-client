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
