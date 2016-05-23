
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.EnhancedDurability;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase : IOperation
    {
        private bool _timedOut;
        protected IByteConverter Converter;
        protected Flags Flags;
        private Dictionary<TimingLevel, IOperationTimer> _timers;
        private const int DefaultOffset = 24;
        public const int HeaderLength = 24;
        public const int DefaultRetries = 2;
        protected static MutationToken DefaultMutationToken = new MutationToken(null, -1, -1, -1);

        protected OperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
        {
            Key = key;
            Transcoder = transcoder;
            Opaque = opaque;
            CreationTime = DateTime.UtcNow;
            Timeout = timeout;
            VBucket = vBucket;
            Converter = transcoder.Converter;
            MaxRetries = DefaultRetries;
            Data = new MemoryStream();
            Header = new OperationHeader {Status = ResponseStatus.None};
        }

        protected OperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : this(key, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
        }

        public Func<TimingLevel, object, IOperationTimer> Timer { get; set; }
        public abstract OperationCode OperationCode { get; }
        public OperationHeader Header { get; set; }
        public OperationBody Body { get; set; }
        public DataFormat Format { get; set; }
        public Compression Compression { get; set; }
        public string Key { get; protected set; }
        public Exception Exception { get; set; }
        public virtual int BodyOffset { get { return DefaultOffset; } }
        public ulong Cas { get; set; }
        public MemoryStream Data { get; set; }
        public byte[] Buffer { get; set; }
        public uint Opaque { get; protected set; }
        public IVBucket VBucket { get; set; }
        public int LengthReceived { get; protected set; }
        public int TotalLength { get { return Header.TotalLength; }}
        public virtual bool Success { get { return Header.Status == ResponseStatus.Success && Exception == null; } }
        public uint Expires { get; set; }
        public int Attempts { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreationTime { get; set; }
        public uint Timeout { get; set; }
        public byte[] WriteBuffer { get; set; }
        public Func<SocketAsyncState, Task> Completed { get; set; }

        public virtual void Reset()
        {
            Reset(ResponseStatus.Success);
        }

        public void BeginTimer(TimingLevel level)
        {
            if (Timer != null)
            {
                var timer = Timer(level, this);
                if (_timers == null)
                {
                    _timers = new Dictionary<TimingLevel, IOperationTimer>();
                }
                if (!_timers.ContainsKey(level))
                {
                    _timers.Add(level, timer);
                }
            }
        }

        public void EndTimer(TimingLevel level)
        {
            if (_timers != null && _timers.ContainsKey(level))
            {
                IOperationTimer timer;
                if(_timers.TryGetValue(level, out timer))
                {
                    if (timer != null)
                    {
                        timer.Dispose();
                        _timers.Remove(level);
                    }
                }
            }
        }

        public virtual void Reset(ResponseStatus status)
        {
            if (Data != null)
            {
                Data.Dispose();
            }
            Data = new MemoryStream();
            LengthReceived = 0;

            Header = new OperationHeader
            {
                Magic = Header.Magic,
                OperationCode = OperationCode,
                Cas = Header.Cas,
                BodyLength = Header.BodyLength,
                Key = Key,
                Status = status
            };
        }

        public virtual void HandleClientError(string message, ResponseStatus responseStatus)
        {
            Reset(responseStatus);
            var msgBytes = Encoding.UTF8.GetBytes(message);
            LengthReceived += msgBytes.Length;
            if (Data == null)
            {
                Data = new MemoryStream();
            }
            Data.Write(msgBytes, 0, msgBytes.Length);
        }

        public virtual void Read(byte[] buffer, int offset, int length)
        {
            if (Header.BodyLength == 0 && buffer.Length >= HeaderIndexFor.HeaderLength)
            {
                Header = new OperationHeader
                {
                    Magic = Converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = Converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = Converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    Status = (ResponseStatus) Converter.ToInt16(buffer, HeaderIndexFor.Status),
                    BodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = Converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };

                if (Opaque != Header.Opaque)
                {
                    var msg = string.Format("Expected opaque {0} but got {1}", Opaque, Header.Opaque);
                    HandleClientError(msg, ResponseStatus.ClientFailure);
                }
            }
            LengthReceived += length;
            Data.Write(buffer, offset, length);
        }

        public async virtual Task ReadAsync(byte[] buffer, int offset, int length)
        {
            if (Header.BodyLength == 0 && buffer.Length >= HeaderIndexFor.HeaderLength)
            {
                Header = new OperationHeader
                {
                    Magic = Converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = Converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = Converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    Status = (ResponseStatus)Converter.ToInt16(buffer, HeaderIndexFor.Status),
                    BodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = Converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };
            }
            if (Opaque != Header.Opaque)
            {
                var msg = string.Format("Expected opaque {0} but got {1}", Opaque, Header.Opaque);
                HandleClientError(msg, ResponseStatus.ClientFailure);
            }

            await Data.WriteAsync(buffer, offset, length);
            LengthReceived += length;
        }

        public virtual Task<byte[]> WriteAsync()
        {
            var tcs = new TaskCompletionSource<byte[]>();
            try
            {
                tcs.SetResult(Write());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return tcs.Task;
        }

        public virtual byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];
            var totalLength = extras.GetLengthSafe() + key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt16((short)key.GetLengthSafe(), header, HeaderIndexFor.KeyLength);
            Converter.FromByte((byte)extras.GetLengthSafe(), header, HeaderIndexFor.ExtrasLength);

            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, header, HeaderIndexFor.VBucket);
            }

            Converter.FromInt32(totalLength, header, HeaderIndexFor.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderIndexFor.Opaque);
            Converter.FromUInt64(Cas, header, HeaderIndexFor.Cas);

            return header;
        }

        public virtual void ReadExtras(byte[] buffer)
        {
            if (buffer.Length > 24)
            {
                var format = new byte();
                var flags = Converter.ToByte(buffer, 24);
                Converter.SetBit(ref format, 0, Converter.GetBit(flags, 0));
                Converter.SetBit(ref format, 1, Converter.GetBit(flags, 1));
                Converter.SetBit(ref format, 2, Converter.GetBit(flags, 2));
                Converter.SetBit(ref format, 3, Converter.GetBit(flags, 3));

                var compression = new byte();
                Converter.SetBit(ref compression, 4, Converter.GetBit(flags, 4));
                Converter.SetBit(ref compression, 5, Converter.GetBit(flags, 5));
                Converter.SetBit(ref compression, 6, Converter.GetBit(flags, 6));

                var typeCode = (TypeCode)(Converter.ToUInt16(buffer, 26) & 0xff);
                Format = (DataFormat)format;
                Compression = (Compression) compression;
                Flags.DataFormat = Format;
                Flags.Compression = Compression;
                Flags.TypeCode = typeCode;
                Expires = Converter.ToUInt32(buffer, 25);
            }
        }

        public virtual byte[] CreateKey()
        {
            var length = Encoding.UTF8.GetByteCount(Key);
            var buffer = new byte[length];
            Converter.FromString(Key, buffer, 0);
            return buffer;
        }

        public IOperationResult GetResult()
        {
            var result = new OperationResult();
            try
            {
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetResponseStatus();
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;

                //clean up and set to null
                if (!result.IsNmv())
                {
                    Data.Dispose();
                    Data = null;
                }
            }
            catch (Exception e)
            {
                result.Exception = e;
                result.Success = false;
                result.Status = ResponseStatus.ClientFailure;
            }
            finally
            {
                if (Data != null && !result.IsNmv())
                {
                    Data.Dispose();
                }
            }
            return result;
        }

        public virtual bool GetSuccess()
        {
            return Header.Status == ResponseStatus.Success && Exception == null;
        }

        public virtual ResponseStatus GetResponseStatus()
        {
            var status = Header.Status;
            if (Exception != null && status == ResponseStatus.Success)
            {
                status = ResponseStatus.ClientFailure;
            }
            return status;
        }

        public virtual string GetMessage()
        {
            var message = string.Empty;
            if (Success) return message;
            if (Header.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                message = ResponseStatus.VBucketBelongsToAnotherServer.ToString();
            }
            else
            {
                if (Exception == null)
                {
                    try
                    {
                        if (Header.Status != ResponseStatus.Success)
                        {
                            if (Data == null || Data.Length == 0)
                            {
                                message = string.Empty;
                            }
                            else
                            {
                                var buffer = Data.ToArray();
                                if (buffer.Length > 0 && TotalLength == 24)
                                {
                                    message = Converter.ToString(buffer, 0, buffer.Length);
                                }
                                else
                                {
                                    message = Converter.ToString(buffer, 24, Math.Min(buffer.Length - 24, TotalLength - 24));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        message = e.Message;
                    }
                }
                else
                {
                    message = Exception.Message;
                }
            }
            return message;
        }

        public virtual IBucketConfig GetConfig()
        {
            IBucketConfig config = null;
            if (GetResponseStatus() == ResponseStatus.VBucketBelongsToAnotherServer && Data != null)
            {
                var offset = HeaderLength + Header.ExtrasLength;
                var length = Header.BodyLength - Header.ExtrasLength;

                //Override any flags settings since the body of the response has changed to a config
                config = Transcoder.Decode<BucketConfig>(Data.ToArray(), offset, length, new Flags
                {
                    Compression = Compression.None,
                    DataFormat = DataFormat.Json,
                    TypeCode = TypeCode.Object
                }, OperationCode);
            }
            return config;
        }

        public virtual bool CanRetry()
        {
            return Cas > 0;
        }

        public bool TimedOut()
        {
            if (_timedOut) return _timedOut;

            var elasped = DateTime.UtcNow.Subtract(CreationTime).TotalMilliseconds;
            if (elasped >= Timeout)
            {
                _timedOut = true;
            }
            return _timedOut;
        }

        public ITypeTranscoder Transcoder { get; protected set; }
        public MutationToken MutationToken { get; protected set; }

        public virtual byte[] CreateExtras()
        {
            return new byte[0];
        }

        public virtual byte[] CreateBody()
        {
            return new byte[0];
        }

        public virtual byte[] Write()
        {
            var extras = CreateExtras();
            var key = CreateKey();
            var body = CreateBody();
            var header = CreateHeader(extras, body, key);

            var buffer = new byte[extras.GetLengthSafe() +
                                  body.GetLengthSafe() +
                                  key.GetLengthSafe() +
                                  header.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length + extras.Length + key.Length, body.Length);

            return buffer;
        }

        public virtual IOperation Clone()
        {
            throw new NotImplementedException();
        }

        public uint LastConfigRevisionTried { get; set; }

        public IPEndPoint CurrentHost { get; set; }

#region "New" Write API Methods - override and implement these methods for new operations

        public virtual byte[] AllocateBuffer(int length)
        {
            return new byte[length];
        }

        public virtual void WriteHeader(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public virtual void WriteBody(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WriteExtras(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WriteKey(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WritePath(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        private short _keyLength;

        public virtual short KeyLength
        {
            get
            {
                if (_keyLength == 0)
                {
                    _keyLength = (short)Encoding.UTF8.GetByteCount(Key);
                }
                return _keyLength;
            }
        }

        public virtual short ExtrasLength { get; protected set; }

        protected int _bodyLength = 0;
        public virtual int BodyLength
        {
            get
            {
                if (_bodyLength == 0)
                {
                    BodyBytes = CreateBody();
                    _bodyLength = BodyBytes.Length;
                }
                return _bodyLength;
            }
        }

        public virtual byte[] BodyBytes { get; protected set; }

        public virtual short PathLength { get; protected set; }
    }

#endregion
}
