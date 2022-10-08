using Couchbase.Utils;
using System;
using Couchbase.Core.IO.Converters;
using System.Threading.Tasks.Dataflow;
using Couchbase.KeyValue.RangeScan;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Couchbase.Core.Utils;

namespace Couchbase.Core.IO.Operations.RangeScan
{
    internal class RangeScanContinue : OperationBase<SlicedMemoryOwner<byte>>, IObserver<AsyncStateStreaming>
    {
        /// <summary>
        /// Maximum key/document count to return (when 0 there is no limit)
        /// </summary>
        public uint ItemLimit { get; set; } = 0;

        /// <summary>
        /// Maximum time (ms) for the scan to keep returning key/documents (when 0 there is no time limit)
        /// </summary>
        public uint TimeLimit { get; set; } = 0;

        /// <summary>
        /// Bytes to return (when 0 there is no limit).
        /// </summary>
        public uint ByteLimit { get; set; } = 0;

        public override OpCode OpCode => OpCode.RangeScanContinue;

        public override bool RequiresVBucketId => false;

        public override bool CanStream => true;



        protected override void WriteKey(OperationBuilder builder)
        {
            //no key
        }

        protected override void WriteBody(OperationBuilder builder)
        {
            // No body
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            //Todo: implement ByteBatchLimit and ItemLimit from options
            Span<byte> extras = stackalloc byte[28];
            Content.Memory.Span.CopyTo(extras);
            ByteConverter.FromUInt32(ItemLimit, extras.Slice(16)); //write item limit 16
            ByteConverter.FromUInt32(TimeLimit, extras.Slice(20)); //write time limit 20
            ByteConverter.FromUInt32(ByteLimit, extras.Slice(24)); //write byte limit 24
            builder.Write(extras);
        }

        public override void Reset()
        {
            //We need to reuse the opaque so the server knows which range scan to continue.
            var opaque = Opaque;

            //update the opaque created by the Reset() call to our original one.
            Opaque = opaque;
        }

        public void OnCompleted()
        {
            _channel.Writer.Complete();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(AsyncStateStreaming value)
        {
            var data = value._response.Slice(Header.BodyOffset + 3);
            value._response = SlicedMemoryOwner<byte>.Empty;

            _channel.Writer.WriteAsync(data).ConfigureAwait(false);
        }

        private Channel<SlicedMemoryOwner<byte>> _channel = Channel.CreateUnbounded<SlicedMemoryOwner<byte>>();

        public async IAsyncEnumerable<ScanResult> Read()
        {
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                var data = await _channel.Reader.ReadAsync().ConfigureAwait(false);

                var length = data.Memory.Length;
                var processed = 0;

                while (processed < length)
                {
                    ScanResult scanResult = null;
                    try
                    {
                        var header = data.Slice(processed, 25);
                        var flags = Flags.Read(header.Slice(0, 4).Memory.Span);
                        var expiry = ByteConverter.ToInt32(header.Slice(4, 4).Memory.Span, true);
                        var seqno = ByteConverter.ToInt32(header.Slice(8,8).Memory.Span, true);
                        var cas = ByteConverter.ToUInt64(header.Slice(16, 8).Memory.Span, true);

                        processed += header.Memory.Length + 1; //+1 Comes from the DataType Byte which is not used by the .NET client since compression is not used.

                        var keyLength = Leb128.Read(data.Slice(processed).Memory.Span);
                        var key = ByteConverter.ToString(data.Slice(processed + keyLength.Item2, (int)keyLength.Item1).Memory.Span);

                        processed += (int)keyLength.Item2 + (int)keyLength.Item1;

                        var bodyLength = Leb128.Read(data.Slice(processed).Memory.Span);
                        var body = data.Slice(processed + bodyLength.Item2, (int)bodyLength.Item1);

                        processed += (int)bodyLength.Item1 + (int)bodyLength.Item2;

                        scanResult = new ScanResult(body, key, false, DateTime.UtcNow.AddTicks(expiry), seqno, cas, OpCode, Transcoder, flags);
                    }
                    catch(Exception e)
                    {
                        var ex = e.ToString();
                        yield break;
                    }
                    yield return scanResult;
                }
            }
        }

        public async IAsyncEnumerable<ScanResult> ReadKeys()
        {
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                var data = await _channel.Reader.ReadAsync().ConfigureAwait(false);
                var length = data.Memory.Length;
                var processed = 0;

                while (processed < length)
                {
                    ScanResult scanResult = null;
                    try
                    {
                        (long, short) keyLength = Leb128.Read(data.Slice(processed).Memory.Span);
                        var key = ByteConverter.ToString(data.Slice(processed += keyLength.Item2, (int)keyLength.Item1).Memory.Span);
                        processed += (int)keyLength.Item1;

                        scanResult = new ScanResult(SlicedMemoryOwner<byte>.Empty, key, true, DateTime.UtcNow, 0, 0, OpCode, Transcoder);
                    }
                    catch (Exception e)
                    {
                        var ex = e.ToString();
                        yield break;
                    }
                    yield return scanResult;
                }
            }
        }
    }

    internal class RangeScanState
    {
        public RangeScanState(uint opaque)
        {
            Opaque = opaque;
        }

        public uint Opaque { get; private set; }

        public ResponseStatus Status { get; set; }

        public SlicedMemoryOwner<byte> Response { get; set; }
    }
}
