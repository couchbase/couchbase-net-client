using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    internal class TypecastingEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator _internalEnumerator;

        public TypecastingEnumerator(IEnumerator internalEnumerator)
        {
            if (internalEnumerator == null)
            {
                throw new ArgumentNullException("internalEnumerator");
            }

            _internalEnumerator = internalEnumerator;
        }

        public void Dispose()
        {
            // IEnumerator doesn't implement Dispose
        }

        public bool MoveNext()
        {
            return _internalEnumerator.MoveNext();
        }

        public void Reset()
        {
            _internalEnumerator.Reset();
        }

        public T Current
        {
            get { return (T) _internalEnumerator.Current; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
    }
}
