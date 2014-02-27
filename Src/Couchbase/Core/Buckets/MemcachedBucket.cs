 using System;
 using Couchbase.IO.Operations;

namespace Couchbase.Core.Buckets
{
    public class MemcachedBucket : IBucket
    {
        private IKeyMapper _keyMapper;

        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public IOperationResult<T> Insert<T>(string key, T value)
        {
            throw new NotImplementedException();
        }


        public IOperationResult<T> Get<T>(string key)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
