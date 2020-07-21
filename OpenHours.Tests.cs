using System;
using Xunit;

namespace Chneau.Time
{
    public class Tests
    {
        [Fact]
        public void TestTwo()
        {
            var oh = new OpenHours();
            Assert.Equal(2, oh.Two()); // success
            Assert.Equal(2, oh.Two() + 40); // fails
        }
    }
}
