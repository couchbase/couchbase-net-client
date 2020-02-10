using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core.Logging
{
    public class RedactableArgumentTests
    {
        [Fact]
        public void When_Redaction_Disabled_No_Redaction_Occurs()
        {
            var ctx = new ClusterContext();
            ctx.ClusterOptions.RedactionLevel = RedactionLevel.None;
            var redactor = new Redactor(ctx);

            Assert.Equal("1", redactor.UserData("1").ToString());
            Assert.Equal(null, redactor.MetaData(null));
            Assert.Equal("system", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void When_User_Redaction_Redact_Partial()
        {
            var ctx = new ClusterContext();
            ctx.ClusterOptions.RedactionLevel = RedactionLevel.Partial;
            var redactor = new Redactor(ctx);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("meta", redactor.MetaData("meta").ToString());
            Assert.Equal("system", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void When_Full_Redaction_Redact_Everything()
        {
            var ctx = new ClusterContext();
            ctx.ClusterOptions.RedactionLevel = RedactionLevel.Full;
            var redactor = new Redactor(ctx);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("<md>meta</md>", redactor.MetaData("meta").ToString());
            Assert.Equal("<sd>system</sd>", redactor.SystemData("system").ToString());
        }
    }
}
