using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class OptionsTests
    {
        [Fact]
        public void GetOptions_Action_Succeeds()
        {
            Action<GetOptions> optionsAction = get =>
            {
                get.WithCreatePath(true);
                get.WithTimeout(new TimeSpan(1, 1, 1));
            };

            var options = new GetOptions();
            optionsAction(options);

            Assert.Equal(new TimeSpan(1, 1, 1), options.Timeout);
            Assert.True(options.CreatePath);
        }

        [Fact]
        public void StructOptions_Action_Failed()
        {
            Action<StructOptions> optionsAction = get =>
            {
                get.WithCreatePath(true);
                get.WithTimeout(new TimeSpan(1, 1, 1));
            };

            var options = new StructOptions();
            optionsAction(options);

            Assert.Null(options.Timeout);
            Assert.False(options.CreatePath);
        }

        public struct StructOptions
        {
            public bool CreatePath { get; set; }

            public TimeSpan? Timeout { get; set; }

            public StructOptions WithTimeout(TimeSpan timeout)
            {
                Timeout = timeout;
                return this;
            }

            public StructOptions WithCreatePath(bool createPath)
            {
                CreatePath = createPath;
                return this;
            }
        }
    }
}
