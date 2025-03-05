using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Support;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class StateFlagsTests
{
    [Fact]
    public void StateFlags_StartsOutWithNothingSet()
    {
        var flags = new StateFlags();
        Assert.Equal(TransactionOperationFailedException.FinalError.None, flags.GetFinalError());
        Assert.True(flags.IsFlagSet(StateFlags.BehaviorFlags.NotSet));
    }

    [Fact]
    public void StateFlags_BehaviorFlagsCanBeOrdTogether()
    {
        var flags = new StateFlags();
        flags.SetFlags(StateFlags.BehaviorFlags.CommitNotAllowed | StateFlags.BehaviorFlags.AppRollbackNotAllowed,
            TransactionOperationFailedException.FinalError.None);
        Assert.Equal(TransactionOperationFailedException.FinalError.None, flags.GetFinalError());
        Assert.True(flags.IsFlagSet(StateFlags.BehaviorFlags.CommitNotAllowed));
        Assert.True(flags.IsFlagSet(StateFlags.BehaviorFlags.AppRollbackNotAllowed));
        Assert.False(flags.IsFlagSet(StateFlags.BehaviorFlags.ShouldNotRetry));
        Assert.False(flags.IsFlagSet(StateFlags.BehaviorFlags.ShouldNotRollback));
    }

    [Fact]
    public void StateFlags_CanSetAndGetFinalError()
    {
        var flags = new StateFlags();
        flags.SetFlags(StateFlags.BehaviorFlags.CommitNotAllowed,
            TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous, flags.GetFinalError());
    }

    [Fact]
    public void StateFlags_FinalErrorHasAPrecedence()
    {
        var flags = new StateFlags();
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionFailed);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionFailed, flags.GetFinalError());
        // now setting this to anything else overrides it
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionExpired);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionExpired, flags.GetFinalError());
        // Setting it back to TransactionFailed does nothing
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionFailed);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionExpired, flags.GetFinalError());
        // now set to CommitAmbiguous
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous, flags.GetFinalError());
        // Setting back to Expired does nothing
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionExpired);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous, flags.GetFinalError());
        // now set to FailedPostCommit which has precedence over everything
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionFailedPostCommit);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionFailedPostCommit, flags.GetFinalError());
        flags.SetFlags(StateFlags.BehaviorFlags.NotSet,
            TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionFailedPostCommit, flags.GetFinalError());
    }
}
