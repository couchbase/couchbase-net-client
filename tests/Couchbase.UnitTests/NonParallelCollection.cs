using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Couchbase.UnitTests
{
    [CollectionDefinition("NonParallel", DisableParallelization = true)]
    public class NonParallelCollection
    {
    }
}
