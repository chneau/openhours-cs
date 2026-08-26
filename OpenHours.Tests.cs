using System.Text.Json;
using Xunit;

namespace Chneau.Time;

public class OpenHoursTests
{
    private static readonly DateTime MondayMidnight = new(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc); // Monday

    [Fact]
    public void TestBasicFunctionality()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-17:00");
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(10)));
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(7).AddMinutes(59)));
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(17)));
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(5).AddHours(10))); // Saturday
    }

    [Fact]
    public void TestMultipleIntervals()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-12:00, 13:00-17:00");
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(12).AddMinutes(30)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(14)));
    }

    [Fact]
    public void TestWeekendRules()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-17:00; Sa 08:00-12:00");
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(5).AddHours(10)));
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(5).AddHours(14)));
    }

    [Fact]
    public void TestExclusions()
    {
        var oh = OpenHours.Parse("Mo-Su 00:00-24:00; Tu 12:00-13:00 off");
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(1).AddHours(12).AddMinutes(30)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(1).AddHours(14)));
    }

    [Fact]
    public void TestAlwaysOpen()
    {
        var oh = OpenHours.Parse("24/7");
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(6).AddHours(23).AddMinutes(59)));
    }

    [Fact]
    public void TestOvernightShifts()
    {
        var oh = OpenHours.Parse("Mo 22:00-04:00");
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(21).AddMinutes(59)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(22)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(23).AddMinutes(30)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(1).AddMinutes(30)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(1).AddHours(3).AddMinutes(59)));
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(1).AddHours(4)));
    }

    [Fact]
    public void TestSundayOvernightToMonday()
    {
        var oh = OpenHours.Parse("Su 22:00-04:00");
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(6).AddHours(21).AddMinutes(59)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(6).AddHours(22)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(2))); // Monday early morning
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(4)));
    }

    [Fact]
    public void TestAdvancedSyntax()
    {
        var oh1 = OpenHours.Parse("Mo, Tu, We 08:00-12:00");
        Assert.True(oh1.IsOpen(MondayMidnight.AddHours(10)));
        Assert.True(oh1.IsOpen(MondayMidnight.AddDays(1).AddHours(10)));
        Assert.True(oh1.IsOpen(MondayMidnight.AddDays(2).AddHours(10)));
        Assert.False(oh1.IsOpen(MondayMidnight.AddDays(3).AddHours(10)));

        var oh2 = OpenHours.Parse("Monday-Friday 08:00-17:00");
        Assert.True(oh2.IsOpen(MondayMidnight.AddDays(4).AddHours(10)));
        Assert.False(oh2.IsOpen(MondayMidnight.AddDays(5).AddHours(10)));
    }

    [Fact]
    public void TestCurrentShiftEnd()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-17:00");
        var end = oh.GetCurrentShiftEnd(MondayMidnight.AddHours(10));
        Assert.Equal(MondayMidnight.AddHours(17), end);
        Assert.Null(oh.GetCurrentShiftEnd(MondayMidnight.AddHours(17).AddMinutes(30)));
    }

    [Fact]
    public void TestTimeToOpen()
    {
        var oh = OpenHours.Parse("Mo 08:00-16:00");
        var wait = oh.GetTimeToOpen(MondayMidnight.AddDays(1).AddHours(8)); // Tuesday 8am
        Assert.Equal(TimeSpan.FromDays(6), wait);
    }

    [Fact]
    public void TestTimeToOpenForDuration()
    {
        var oh = OpenHours.Parse("10:00-12:00");
        var wait1 = oh.GetTimeToOpenForDuration(MondayMidnight.AddHours(8), TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromHours(2), wait1);

        var wait2 = oh.GetTimeToOpenForDuration(MondayMidnight.AddHours(11), TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromHours(23), wait2);
    }

    [Fact]
    public void TestWhen()
    {
        var oh = OpenHours.Parse("Mo 10:00-15:00");
        var when1 = oh.When(MondayMidnight.AddHours(11), TimeSpan.FromHours(4));
        Assert.Equal(MondayMidnight.AddHours(11), when1);
    }

    [Fact]
    public void TestNextDurAndNextDate()
    {
        var oh = OpenHours.Parse("Mo 08:00-18:00");
        var (isOpen, dur) = oh.NextDur(MondayMidnight.AddHours(10));
        Assert.True(isOpen);
        Assert.Equal(TimeSpan.FromHours(8), dur);

        var (isOpenNext, nextDate) = oh.NextDate(MondayMidnight.AddHours(10));
        Assert.True(isOpenNext);
        Assert.Equal(MondayMidnight.AddHours(18), nextDate);
    }

    [Fact]
    public void TestJsonSerialization()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-17:00");
        string json = JsonSerializer.Serialize(oh, OpenHoursJsonContext.Default.OpenHours);
        Assert.Equal("\"Mo-Fr 08:00-17:00\"", json);

        var deserialized = JsonSerializer.Deserialize(json, OpenHoursJsonContext.Default.OpenHours);
        Assert.NotNull(deserialized);
        Assert.Equal(oh.Raw, deserialized.Raw);
    }

    [Fact]
    public void TestRunBenchmarkSuite()
    {
        using var sw = new StringWriter();
        OpenHoursBenchmarks.RunBenchmarks(sw);
        string output = sw.ToString();
        Assert.Contains("Running OpenHours Benchmarks", output);
        Assert.Contains("Stress Test", output);
    }
}
