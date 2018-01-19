using Couchbase.Logging;
using NUnit.Framework;

namespace Couchbase.UnitTests.Logging
{
    [TestFixture]
    public class RedactableArgumentTests
    {
        [Test]
        public void When_Redaction_Disabled_No_Redaction_Occurs()
        {
            LogManager.RedactionLevel = RedactionLevel.None;
            Assert.AreEqual("1", RedactableArgument.User("1").ToString());
            Assert.AreEqual(null, RedactableArgument.Meta(null).ToString());
            Assert.AreEqual("system", RedactableArgument.System("system").ToString());
        }

        [Test]
        public void When_User_Redaction_Redact_Partial()
        {
            LogManager.RedactionLevel = RedactionLevel.Partial;
            Assert.AreEqual("<ud>user</ud>", RedactableArgument.User("user").ToString());
            Assert.AreEqual("meta", RedactableArgument.Meta("meta").ToString());
            Assert.AreEqual("system", RedactableArgument.System("system").ToString());
        }

        [Test]
        public void When_Full_Redaction_Redact_Everything()
        {
            LogManager.RedactionLevel = RedactionLevel.Full;
            Assert.AreEqual("<ud>user</ud>", RedactableArgument.User("user").ToString());
            Assert.AreEqual("<md>meta</md>", RedactableArgument.Meta("meta").ToString());
            Assert.AreEqual("<sd>system</sd>", RedactableArgument.System("system").ToString());
        }
    }
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
#endregion
