﻿using System;
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
        public SlowSet(string key, T value, IVBucket vBucket, IByteConverter converter)
            : base(key, value, vBucket, converter)
        {
        }

        public SlowSet(string key, T value, ITypeTranscoder transcoder, IVBucket vBucket, IByteConverter converter)
            : base(key, value, transcoder, vBucket, converter, SequenceGenerator.GetNext(), DefaultTimeout)
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

        public override IOperation<T> Clone()
        {
            var cloned = new SlowSet<T>(Key, RawValue, Transcoder, VBucket, Converter)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
