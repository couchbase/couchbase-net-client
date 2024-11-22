
using System;
using System.Threading;
using Couchbase.Integrated.Transactions.Error;
using Couchbase.Integrated.Transactions.Error.External;
using FinalErrorToRaise = Couchbase.Integrated.Transactions.Error.External.TransactionOperationFailedException.FinalErrorToRaise;
namespace Couchbase.Integrated.Transactions.Util;
internal class StateBits
{
    // the spec calls for statebits to be 8 bits
    // but .NET Standard 2.0 only supports Interlocked properly on a 64-bit value
    // Since we have bits to play with, we just store the BehaviorFlags in the second byte and the FinalError in the
    // third byte, ignoring the sign in the first byte
    // If we weren't limited to old versions, we could use Interlocked.Or and Interlocked.And
    private long _state64 = 0;
    private const long PosBehaviorFlags = 1;
    private const long PosFinalError = 2;
    public FinalErrorToRaise FinalError
    {
        get
        {
            var state64 = Interlocked.Read(ref _state64);
            var bytes = BitConverter.GetBytes(state64);
            var finalError = (FinalErrorToRaise)bytes[PosFinalError];
            return finalError;
        }
    }
    public bool HasFlag(params BehaviorFlags[] anyFlag)
    {
        BehaviorFlags combinedFlags = BehaviorFlags.NotSet;
        foreach (var flag in anyFlag)
        {
            combinedFlags |= flag;
        }
        return HasFlag(combinedFlags);
    }
    public bool HasFlag(BehaviorFlags flags)
    {
        // NOTE:  HasFlag(NotSet) will always return true
        var currentFlags = ExtractBehaviorFlags();
        return currentFlags.HasFlag(flags);
    }
    private BehaviorFlags ExtractBehaviorFlags()
    {
        long stateBits64 = Interlocked.Read(ref _state64);
        var bytes = BitConverter.GetBytes(stateBits64);
        BehaviorFlags currentFlags = (BehaviorFlags)bytes[PosBehaviorFlags];
        return currentFlags;
    }
    public void SetStateBits(BehaviorFlags newBehaviorFlags,
        FinalErrorToRaise finalError = FinalErrorToRaise.TransactionSuccess)
    {
        bool atomicWriteFailed = false;
        do
        {
            long oldState64 = Interlocked.Read(ref _state64);
            var bytes = BitConverter.GetBytes(oldState64);
            BehaviorFlags oldBehaviorFlags = (BehaviorFlags)bytes[PosBehaviorFlags];
            FinalErrorToRaise oldFinalError = (FinalErrorToRaise)bytes[PosFinalError];
            // merge the behavior flags
            bytes[PosBehaviorFlags] = (byte)(newBehaviorFlags | oldBehaviorFlags);
            if (finalError > oldFinalError)
            {
                bytes[PosFinalError] = (byte)finalError;
            }
            long newState64 = BitConverter.ToInt64(bytes, 0);
            var finalStateBits = Interlocked.CompareExchange(ref _state64, newState64, oldState64);
            atomicWriteFailed = finalStateBits != newState64;
        } while (atomicWriteFailed);
    }
    public override string ToString() => $"{ExtractBehaviorFlags()}::{FinalError}";
    [Flags]
    internal enum BehaviorFlags : byte
    {
        NotSet = 0,
        CommitNotAllowed = 0x1,
        AppRollbackNotAllowed = 0x2,
        ShouldNotRollback = 0x4,
        ShouldNotRetry = 0x8,
    }
    public void SetFromException(TransactionOperationFailedException err)
    {
        if (err is { Cause: ConcurrentOperationsDetectedOnSameDocumentException, CausingErrorClass: ErrorClass.FailCasMismatch })
        {
            // special case for Create Staged Insert -> Doc Already Exist when staging insert -> Else if the doc is not
            // in a transaction
            return;
        }
        var newFlags = StateBits.BehaviorFlags.NotSet;
        if (!err.AutoRollbackAttempt)
        {
            newFlags |= StateBits.BehaviorFlags.ShouldNotRollback;
        }
        if (!err.RetryTransaction)
        {
            newFlags |= StateBits.BehaviorFlags.ShouldNotRetry;
        }
        SetStateBits(newFlags, finalError: err.ToRaise);
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
