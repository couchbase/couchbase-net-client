using System;
using System.Linq;
using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations;

public class KeyEncodingTests
{
    [Fact]
    public void Test_Key_Size_Exceeds_250bytes_Throws_InvalidArgument()
    {
        var bytes = Enumerable.Repeat((byte)0, 251).ToArray();
        var key = Encoding.UTF8.GetString(bytes);
        try
        {
            var op = new BasicTestOperation(key);
        }
        catch (Exception e)
        {
            Assert.IsType<InvalidArgumentException>(e);
        }
    }

    [Fact]
    public void Test_Null_Key_Size_Throws_InvalidArgument()
    {
        var key = Encoding.UTF8.GetString(new byte[1]);
        try
        {
            var op = new BasicTestOperation(key);
        }
        catch (Exception e)
        {
            Assert.IsType<InvalidArgumentException>(e);
        }
    }
}

internal class BasicTestOperation : OperationBase
{
    public BasicTestOperation(string key)
    {
        Key = key;
    }
    public override OpCode OpCode { get; }
}
