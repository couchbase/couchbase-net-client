using System;
using Couchbase.Utils;

namespace Couchbase.IO
{
    public class MissingKeyException : ArgumentException
    {
        public MissingKeyException()
            : base(ExceptionUtil.EmptyKeyErrorMsg)
        {
        }
    }
}
