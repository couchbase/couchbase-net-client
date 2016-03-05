using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.IO.SubDocument
{
    internal interface ISubdocInvoker
    {
        IDocumentFragment<T> Invoke<T>(IMutateInBuilder<T> builder);

        IDocumentFragment<T> Invoke<T>(ILookupInBuilder<T> builder);
    }
}
