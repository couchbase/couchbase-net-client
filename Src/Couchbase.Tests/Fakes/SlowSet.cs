using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.Tests.Fakes
{
    /// <summary>
    /// A Set operation that allows you to provide a "sleep time" to slow it down. For testing only.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class SlowSet<T> : OperationBase<T>
    {
        private const uint OperationLifespan = 2500; //ms

        public SlowSet(string key, T value, IVBucket vBucket, ITypeTranscoder transcoder)
            : base(key, value, vBucket, transcoder, SequenceGenerator.GetNext(), OperationLifespan)
        {
        }

        public SlowSet(string key, T value, ITypeTranscoder transcoder, IVBucket vBucket, uint timeout)
            : base(key, value, vBucket, transcoder,  SequenceGenerator.GetNext(), timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Set; }
        }

        public int SleepTime { get; set; }

        public override byte[] Write()
        {
            Thread.Sleep(SleepTime);
            return base.Write();
        }

        public override IOperation Clone()
        {
            var cloned = new SlowSet<T>(Key, RawValue, Transcoder, VBucket, OperationLifespan)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
