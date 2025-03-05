using System;
using System.Threading;
using Couchbase.Client.Transactions.Error.External;

namespace Couchbase.Client.Transactions.Support;

internal class StateFlags
{
   [Flags]
   internal enum BehaviorFlags
   {
       NotSet = 0,
       CommitNotAllowed = 1 << 1,
       AppRollbackNotAllowed = 1 << 2,
       ShouldNotRollback = 1 << 3,
       ShouldNotRetry = 1 << 4,
   }

   private const int BehaviorFlagsMask = 0xf;
   private const int ErrorToRaiseMask = 0xf0;
   private const int ErrorToRaiseShift = 4;

   private int _flags;

   public void SetFlags(BehaviorFlags flagsToSet, TransactionOperationFailedException.FinalError finalError)
   {
       int original, newValue;
       do
       {
           original = Volatile.Read( ref _flags);
           var currentBehaviorFlags = original & BehaviorFlagsMask;
           var newBehaviorFlags = currentBehaviorFlags | ((int)flagsToSet & BehaviorFlagsMask);
           var currentFinalError = (original & ErrorToRaiseMask) >> ErrorToRaiseShift;
           int newErrorToRaise;
           if ((int)finalError > currentFinalError)
           {
               newErrorToRaise = ((int)finalError << ErrorToRaiseShift) & ErrorToRaiseMask;
           } else {
               newErrorToRaise = original & ErrorToRaiseMask;
           }
           newValue = newBehaviorFlags | newErrorToRaise;

       } while (Interlocked.CompareExchange(ref _flags, newValue, original) != original);
   }

   public bool IsFlagSet(BehaviorFlags flag)
   {
       return ((Volatile.Read(ref _flags) & BehaviorFlagsMask) & (int)flag) == (int)flag;
   }

   // This will be useful once we switch to using the FinalError flag to determine the final error to throw.  However
   // currently the existing way works, so we will switch over later.   Let's keep this in place for now.
   public TransactionOperationFailedException.FinalError GetFinalError()
   {
       return (TransactionOperationFailedException.FinalError)((Volatile.Read(ref _flags) & ErrorToRaiseMask) >> ErrorToRaiseShift);
   }

}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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
