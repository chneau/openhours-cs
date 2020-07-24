using System;
using System.Collections.Generic;
using Xunit;

namespace Chneau.Time
{
    public class Tests
    {
        [Theory]
        [InlineData("Mo-Fr 10:00-12:00,12:30-16:00", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData("Mo-Fr 10:00-12:00, 12:30-16:00", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData("Mo-Fr 10:00-12:00 ,12:30-16:00", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData("Mo-Fr 10:00-12:00 , 12:30-16:00", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData(" Mo-Fr 10:00-12:00,12:30-16:00", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData("Mo-Fr 10:00-12:00,12:30-16:00 ", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData(" Mo-Fr 10:00-12:00,12:30-16:00 ", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData(" 	Mo-Fr 10:00-12:00,12:30-16:00	 ", "mo-fr 10:00-12:00,12:30-16:00")]
        [InlineData(" 	 	Mo-Fr 	 10:00-12:00,12:30-16:00 	 	 ", "mo-fr 10:00-12:00,12:30-16:00")]
        public void TestCleanInput(string input, string expected) =>
            Assert.Equal(expected, OpenHours.CleanInput(input));

        [Theory]
        [InlineData("mo", new int[] { 1 })]
        [InlineData("mo,mardi", new int[] { 1 })]
        [InlineData("we,fr", new int[] { 3, 5 })]
        [InlineData("we-fr", new int[] { 3, 4, 5 })]
        [InlineData("mo,we-fr,su", new int[] { 0, 1, 3, 4, 5 })]
        [InlineData("mo-pl", new int[] { })]
        [InlineData("pl,mo", new int[] { 1 })]
        [InlineData("fr-mo", new int[] { 0, 1, 5, 6 })]
        [InlineData("mo-tu,tu,tu-fr,fr", new int[] { 1, 2, 3, 4, 5 })]
        public void InternalTestSimplifyDays(string input, int[] expected) => Assert.Equal(expected, OpenHours.SimplifyDays(input));

    }
}
