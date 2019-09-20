using System;

namespace Couchbase.Services.KeyValue
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
