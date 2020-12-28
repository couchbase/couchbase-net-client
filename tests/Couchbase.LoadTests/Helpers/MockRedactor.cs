using Couchbase.Core.Logging;

namespace Couchbase.LoadTests.Helpers
{
    public class MockRedactor : IRedactor
    {
        public object UserData(object message)
        {
            return message;
        }

        public object MetaData(object message)
        {
            return message;
        }

        public object SystemData(object message)
        {
            return message;
        }
    }
}
