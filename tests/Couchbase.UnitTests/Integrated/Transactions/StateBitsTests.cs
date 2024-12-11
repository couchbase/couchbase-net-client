using System.Collections.Generic;
using System.Linq;
using Xunit;
using Couchbase.Client.Transactions.Util;
using BF = Couchbase.Client.Transactions.Util.StateBits.BehaviorFlags;
using FE = Couchbase.Client.Transactions.Error.External.TransactionOperationFailedException.FinalErrorToRaise;
namespace Couchbase.UnitTests.Integrated.Transactions;
public class StateBitsTests
{
    [Fact]
    public void SetAndRead_BehaviorFlags()
    {
        StateBits sb = new();
        Assert.True(sb.HasFlag(BF.NotSet)); // Expected
        Assert.False(sb.HasFlag(BF.CommitNotAllowed));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry));
        Assert.False(sb.HasFlag(BF.ShouldNotRollback));
        Assert.False(sb.HasFlag(BF.AppRollbackNotAllowed));
        Assert.False(sb.HasFlag(
            BF.ShouldNotRetry,
            BF.ShouldNotRollback,
            BF.CommitNotAllowed,
            BF.AppRollbackNotAllowed));
        sb.SetStateBits(BF.CommitNotAllowed);
        Assert.True(sb.HasFlag(BF.CommitNotAllowed));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry));
        Assert.False(sb.HasFlag(BF.ShouldNotRollback));
        Assert.False(sb.HasFlag(BF.AppRollbackNotAllowed));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry, BF.ShouldNotRollback, BF.AppRollbackNotAllowed));
        sb.SetStateBits(BF.ShouldNotRollback);
        Assert.True(sb.HasFlag(BF.CommitNotAllowed));
        Assert.True(sb.HasFlag(BF.ShouldNotRollback));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry));
        Assert.False(sb.HasFlag(BF.AppRollbackNotAllowed));
        sb.SetStateBits(BF.ShouldNotRetry | BF.AppRollbackNotAllowed);
        Assert.True(sb.HasFlag(BF.CommitNotAllowed));
        Assert.True(sb.HasFlag(BF.ShouldNotRollback));
        Assert.True(sb.HasFlag(BF.ShouldNotRetry));
        Assert.True(sb.HasFlag(BF.AppRollbackNotAllowed));
    }
    [Fact]
    public void SetStateBits_AllBehaviorFlagsForwardOnly()
    {
        // Behavior flags can only be set, not un-set.
        var alLValues = System.Enum.GetValues(typeof(BF)).Cast<BF>().Where(bf => bf != BF.NotSet).ToList();
        var unsetValues = new HashSet<BF>(alLValues);
        var sb = new StateBits();
        foreach (var behaviorFlag in alLValues)
        {
            Assert.False(sb.HasFlag(behaviorFlag));
            sb.SetStateBits(behaviorFlag);
            unsetValues.Remove(behaviorFlag);
            foreach (var bf in unsetValues)
            {
                Assert.False(sb.HasFlag(bf), "should be unset: " + bf.ToString());
            }
            var setValues = unsetValues.Except(alLValues);
            foreach (var bf in setValues)
            {
                Assert.True(sb.HasFlag(bf), "should be set: " + bf.ToString());
            }
        }
    }
    [Fact]
    public void SetFinalError_DoesNotChangeBehaviorFlags()
    {
        StateBits sb = new();
        Assert.Equal(FE.TransactionSuccess, sb.FinalError);
        Assert.False(sb.HasFlag(BF.CommitNotAllowed));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry));
        sb.SetStateBits(BF.CommitNotAllowed, FE.TransactionFailed);
        Assert.Equal(FE.TransactionFailed, sb.FinalError);
        Assert.True(sb.HasFlag(BF.CommitNotAllowed));
        Assert.False(sb.HasFlag(BF.ShouldNotRetry));
    }
    [Fact]
    public void SetFinalError_AlwaysForward()
    {
        StateBits sb = new();
        Assert.Equal(FE.TransactionSuccess, sb.FinalError);
        sb.SetStateBits(BF.NotSet, finalError: FE.TransactionFailed);
        Assert.Equal(FE.TransactionFailed, sb.FinalError);
        // setting "down" should do nothing.
        sb.SetStateBits(BF.CommitNotAllowed, finalError: FE.TransactionSuccess);
        Assert.Equal(FE.TransactionFailed, sb.FinalError);
        // Behavior Flags cannot be unset, only merged, so CommitNotAllowed should still be set.
        // Setting Final Error forwards should be applied
        sb.SetStateBits(BF.NotSet, finalError: FE.TransactionExpired);
        Assert.True(sb.HasFlag(BF.CommitNotAllowed));
        Assert.Equal(FE.TransactionExpired, sb.FinalError);
        // Once set to TransactionFailedPostCommit, FinalError cannot be set to anything lower.
        sb.SetStateBits(BF.NotSet, FE.TransactionFailedPostCommit);
        Assert.Equal(FE.TransactionFailedPostCommit, sb.FinalError);
        sb.SetStateBits(BF.NotSet, FE.TransactionCommitAmbiguous);
        Assert.Equal(FE.TransactionFailedPostCommit, sb.FinalError);
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
