#nullable enable
using System;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Fixtures;

public class DelegatingTestOutputHelper : ITestOutputHelper
{
    private readonly ITestOutputHelper _innerHelper;
    private readonly Action<string>? _beforeWrite;
    private readonly Action<string>? _afterWrite;

    public DelegatingTestOutputHelper(
        ITestOutputHelper innerHelper,
        Action<string>? beforeWrite = null,
        Action<string>? afterWrite = null)
    {
        _innerHelper = innerHelper;
        _beforeWrite = beforeWrite;
        _afterWrite = afterWrite;
    }

    public void WriteLine(string message)
    {
        _beforeWrite?.Invoke(message);
        _innerHelper.WriteLine(message);
        _afterWrite?.Invoke(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        string formatted = format;
        try
        {
            formatted = String.Format(format, args);
        }
        catch
        {
            // ignored
        }

        _beforeWrite?.Invoke(formatted);
        _innerHelper.WriteLine(format, args);
        _afterWrite?.Invoke(formatted);
    }
}
