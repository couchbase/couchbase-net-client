using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO
{
    [TestFixture]
    public class ConnectionBaseTests
    {
        #region CountdownToClose

        [Test]
        public void CountdownToClose_RepeatsMaxCloseAttemptsAndDisposes()
        {
            // Arrange

            var connection = new Mock<ConnectionBase>(null, null, null, null, null)
            {
                CallBase = true
            };

            connection.Object.MarkUsed(true);
            connection.Object.MaxCloseAttempts = 3;
            connection.Object.IsDead = false;

            var tcs = new TaskCompletionSource<int>();
            connection
                .Setup(m => m.Dispose())
                .Callback(() => tcs.SetResult(0));

            // Act

            connection.Object.CountdownToClose(1);

            var gaveUp = tcs.Task.Wait(TimeSpan.FromSeconds(10));

            // Assert

            Assert.True(gaveUp);

            connection.Verify(m => m.IncrementCloseAttempts(), Times.Exactly(connection.Object.MaxCloseAttempts));
        }

        [Test]
        public void CountdownToClose_RepeatsUntilNotInUseAndDisposes()
        {
            // Arrange

            var connection = new Mock<ConnectionBase>(null, null, null, null, null)
            {
                CallBase = true
            };

            connection.Object.MarkUsed(true);
            connection.Object.MaxCloseAttempts = 3;
            connection.Object.IsDead = false;

            var tcs = new TaskCompletionSource<int>();
            connection
                .Setup(m => m.Dispose())
                .Callback(() => tcs.SetResult(0));

            var closeAttempts = 0;
            connection
                .Setup(m => m.IncrementCloseAttempts())
                .Callback(() =>
                {
                    closeAttempts++;

                    if (closeAttempts == 2)
                    {
                        connection.Object.MarkUsed(false);
                    }
                });

            // Act

            connection.Object.CountdownToClose(1);

            var gaveUp = tcs.Task.Wait(TimeSpan.FromSeconds(10));

            // Assert

            Assert.True(gaveUp);
            Assert.AreEqual(closeAttempts, 2);
        }

        #endregion
    }
}
