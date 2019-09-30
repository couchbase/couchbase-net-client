using System;

namespace Couchbase.KeyValue
{
    public class XattrUnknownMacroException : KeyValueException
    {
        public XattrUnknownMacroException()
        {
        }

        public XattrUnknownMacroException(string message)
            : base(message)
        {
        }

        public XattrUnknownMacroException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
