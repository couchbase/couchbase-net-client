using Couchbase.Utils;
using System;
using Couchbase.Core.IO.Converters;
using Couchbase.KeyValue.RangeScan;
using System.Collections.Generic;
using Couchbase.Core.Utils;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Operations.RangeScan
{
    internal class RangeScanContinue : OperationBase<IDictionary<string, IScanResult>>, IObserver<SlicedMemoryOwner<byte>>
    {
        //To hold the intermediate responses
        private List<SlicedMemoryOwner<byte>> _responses = new();

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

        public SlicedMemoryOwner<byte>  Uuid { private get; set; }

        public override OpCode OpCode => OpCode.RangeScanContinue;

        public override bool RequiresVBucketId => false;

        public override bool CanStream => true;

        public bool IdsOnly { get; set; }

        public ILogger<GetResult> Logger { get;set; }

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
            Span<byte> extras = stackalloc byte[28];
            Uuid.Memory.Span.CopyTo(extras);
            ByteConverter.FromUInt32(ItemLimit, extras.Slice(16)); //write item limit 16
            ByteConverter.FromUInt32(TimeLimit, extras.Slice(20)); //write time limit 20
            ByteConverter.FromUInt32(ByteLimit, extras.Slice(24)); //write byte limit 24
            builder.Write(extras);
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            //not needed?
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
            if (IdsOnly)
            {
                ReadKeys();
            }
            else
            {
                ReadKeysAndBody();
            }
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(SlicedMemoryOwner<byte> value)
        {
            _responses.Add(value);
        }

        private void ReadKeys()
        {
            Content = new Dictionary<string, IScanResult>();
            foreach (var response in _responses)
            {
                //First we need to process the MCBP header
                var header = response.Memory.Span.CreateHeader();
                var opaque = ByteConverter.ToUInt32(response.Memory.Span.Slice(HeaderOffsets.Opaque), false);
                var length = header.TotalLength;
                var processed = header.BodyOffset;

                //Then we process each document in the body
                while (processed < length)
                {
                    IScanResult scanResult = null;

                    try
                    {
                        var keyLength = Leb128.Read(response.Slice(processed).Memory.Span);
                        var key = ByteConverter.ToString(response
                            .Slice(processed += keyLength.Length, (int)keyLength.Value).Memory.Span);
                        processed += (int)keyLength.Item1;

                        scanResult = new ScanResult(SlicedMemoryOwner<byte>.Empty, key, true, DateTime.UtcNow, 0, 0,
                            OpCode, Transcoder);
                    }
                    catch(Exception e)
                    {
                        var ex = e.ToString();
                        Logger.LogError(ex);
                    }

                    if (scanResult != null)
                    {
                        Content.Add(scanResult.Id, scanResult);
                    }
                }
            }
        }

        private void ReadKeysAndBody()
        {
            Content = new Dictionary<string, IScanResult>();
            foreach (var response in _responses)
            {
                //First we need to process the MCBP header
                var header = response.Memory.Span.CreateHeader();
                var opaque = ByteConverter.ToUInt32(response.Memory.Span.Slice(HeaderOffsets.Opaque), false);

                //HeaderOffsets.ExtrasLength
                var length = header.TotalLength;
                var processed = header.BodyOffset; //MCBP header (24) + Extras (4) + FlexibleFramingExtras (3)

                //Then we process each document in the body
                while (processed < length)
                {
                    IScanResult scanResult = null;

                    try
                    {
                        var flags = ByteConverter.ToInt32(response.Slice(processed, 4).Memory.Span);
                        var expiry = ByteConverter.ToUInt32(response.Slice(processed += 4, 4).Memory.Span);
                        var seqno = ByteConverter.ToUInt32(response.Slice(processed += 4, 8).Memory.Span);
                        var cas = ByteConverter.ToUInt32(response.Slice(processed += 8, 8).Memory.Span);
                        var dataType = response.Slice(processed += 8, 1).Memory.Span;

                        var keyLength = Leb128.Read(response.Slice(processed += 1).Memory.Span);
                        var key = ByteConverter.ToString(response.Slice(processed += keyLength.Length, (int)keyLength.Value).Memory.Span);

                        Logger.LogDebug("Range Scan processing item {Content.Count} for opaque {opaque} for key {key}", Content.Count, Opaque, Key);//TODO: remove this later

                        var bodyLength = Leb128.Read(response.Slice(processed += (int)keyLength.Value).Memory.Span);
                        SlicedMemoryOwner<byte> body = response.Slice(processed += bodyLength.Length, (int)bodyLength.Value);
                        processed += (int)bodyLength.Value;

                        scanResult = new ScanResult(body, key, false, DateTime.UtcNow.AddTicks(expiry), (int)seqno, cas,
                            OpCode, Transcoder, null);
                    }
                    catch(Exception e)
                    {
                        var ex = e.ToString();
                        Logger.LogError(ex);
                    }

                    if (scanResult != null)
                    {
                        Content.Add(scanResult.Id, scanResult);
                    }
                }
            }
        }
    }
}
