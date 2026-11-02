using System.Net.Security;

namespace Couchbase.ConfigRecorder.Tap;

/// <summary>
/// Wraps the network stream of a plain connection and shows every byte to the tap.
/// Each override calls the inner stream once, so no byte is seen twice.
/// </summary>
internal sealed class TappingStream(Stream inner, ConnectionTap tap) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanSeek => false;
    public override bool CanTimeout => inner.CanTimeout;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read;
        try
        {
            read = inner.Read(buffer, offset, count);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        AfterRead(buffer.AsSpan(offset, read));
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read;
        try
        {
            read = inner.Read(buffer);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        AfterRead(buffer[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ValueTask<int> pending;
        try
        {
            pending = inner.ReadAsync(buffer, cancellationToken);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        if (pending.IsCompletedSuccessfully)
        {
            var read = pending.Result;
            AfterRead(buffer.Span[..read]);
            return new ValueTask<int>(read);
        }

        return AwaitRead(pending, buffer);
    }

    private async ValueTask<int> AwaitRead(ValueTask<int> pending, Memory<byte> buffer)
    {
        int read;
        try
        {
            read = await pending.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        AfterRead(buffer.Span[..read]);
        return read;
    }

    private void AfterRead(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            tap.OnReadEnd(null);
        }
        else
        {
            tap.OnRead(data);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        tap.OnWrite(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        tap.OnWrite(buffer);
        inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        tap.OnWrite(buffer.AsSpan(offset, count));
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        tap.OnWrite(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tap.OnClose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        tap.OnClose();
        await inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// An <see cref="SslStream"/> that shows every decrypted byte to the tap. It stays an SslStream so the
/// SDK still builds an SslConnection, which decides the SASL mechanism.
/// </summary>
/// <remarks>
/// MultiplexingConnection reads through a PipeReader, which calls ReadAsync(Memory), and writes with
/// WriteAsync(ReadOnlyMemory). Those two are tapped. Any other read or write path stops the tap rather
/// than risk a frame boundary that is out of step.
/// </remarks>
internal sealed class TappingSslStream(Stream inner, ConnectionTap tap) : SslStream(inner, leaveInnerStreamOpen: false)
{
    private static readonly NotSupportedException OtherPath = new("The SDK used a stream method that the tap does not watch.");

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ValueTask<int> pending;
        try
        {
            pending = base.ReadAsync(buffer, cancellationToken);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        if (pending.IsCompletedSuccessfully)
        {
            var read = pending.Result;
            AfterRead(buffer.Span[..read]);
            return new ValueTask<int>(read);
        }

        return AwaitRead(pending, buffer);
    }

    private async ValueTask<int> AwaitRead(ValueTask<int> pending, Memory<byte> buffer)
    {
        int read;
        try
        {
            read = await pending.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            tap.OnReadEnd(e);
            throw;
        }

        AfterRead(buffer.Span[..read]);
        return read;
    }

    private void AfterRead(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            tap.OnReadEnd(null);
        }
        else
        {
            tap.OnRead(data);
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        tap.OnWrite(buffer.Span);
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        tap.Stop("read", OtherPath);
        return base.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
        tap.Stop("read", OtherPath);
        return base.ReadByte();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        tap.Stop("read", OtherPath);
        return base.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        tap.Stop("write", OtherPath);
        base.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        tap.Stop("write", OtherPath);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tap.OnClose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        tap.OnClose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
