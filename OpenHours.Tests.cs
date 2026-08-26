using System.Text.Json;
using Xunit;

namespace Chneau.OpenHours;

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
    public void TestEmptySchedule()
    {
        var oh = OpenHours.Parse("");
        Assert.False(oh.IsOpen(MondayMidnight));
        Assert.Null(oh.GetTimeToOpen(MondayMidnight));
        Assert.Null(oh.GetCurrentShiftEnd(MondayMidnight));
        Assert.Empty(oh.Windows);

        var whitespace = OpenHours.Parse("   ");
        Assert.NotNull(whitespace);
        Assert.False(whitespace.IsOpen(MondayMidnight));
    }

    [Fact]
    public void TestInvalidInputFallback()
    {
        var invalid = new[] { "invalid", "Mo invalid", "Mo 25:00-26:00", "Xx 08:00-17:00" };
        foreach (var expr in invalid)
        {
            var oh = OpenHours.Parse(expr);
            Assert.False(oh.IsOpen(MondayMidnight.AddHours(10)), $"expected closed for '{expr}'");
        }
    }

    [Fact]
    public void TestOvernightMultiDay()
    {
        var oh = OpenHours.Parse("Mo-Fr 22:00-04:00");
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(23)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(1).AddHours(2)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(4).AddHours(23)));
        Assert.True(oh.IsOpen(MondayMidnight.AddDays(5).AddHours(2)));
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(5).AddHours(23)));
        Assert.False(oh.IsOpen(MondayMidnight.AddDays(6).AddHours(2)));
    }

    [Fact]
    public void TestOpenEndedInterval()
    {
        var oh = OpenHours.Parse("Mo 10:00+");
        Assert.False(oh.IsOpen(MondayMidnight.AddHours(9)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(10)));
        Assert.True(oh.IsOpen(MondayMidnight.AddHours(23)));
    }

    [Fact]
    public void TestStandaloneKeywordsAndAllDay()
    {
        Assert.False(OpenHours.Parse("closed").IsOpen(MondayMidnight));
        Assert.False(OpenHours.Parse("off").IsOpen(MondayMidnight));
        Assert.True(OpenHours.Parse("open").IsOpen(MondayMidnight));
        Assert.True(OpenHours.Parse("Mo open").IsOpen(MondayMidnight.AddHours(15)));
        Assert.False(OpenHours.Parse("Mo open").IsOpen(MondayMidnight.AddDays(1)));
        Assert.True(OpenHours.Parse("Mo 00:00-00:00").IsOpen(MondayMidnight.AddHours(15)));
        Assert.False(OpenHours.Parse("Mo 00:00-00:00").IsOpen(MondayMidnight.AddDays(1)));
    }

    [Fact]
    public void TestDayOnlyDefaults()
    {
        var mo = OpenHours.Parse("Mo");
        Assert.True(mo.IsOpen(MondayMidnight));
        Assert.True(mo.IsOpen(MondayMidnight.AddHours(23).AddMinutes(59)));
        Assert.False(mo.IsOpen(MondayMidnight.AddDays(1)));

        var moFr = OpenHours.Parse("Mo-Fr");
        Assert.True(moFr.IsOpen(MondayMidnight.AddHours(10)));
        Assert.False(moFr.IsOpen(MondayMidnight.AddDays(5)));
    }

    [Fact]
    public void TestAdvancedDaySyntax()
    {
        var list = OpenHours.Parse("Mo, Tu, We 08:00-12:00");
        Assert.True(list.IsOpen(MondayMidnight.AddHours(10)));
        Assert.True(list.IsOpen(MondayMidnight.AddDays(1).AddHours(10)));
        Assert.True(list.IsOpen(MondayMidnight.AddDays(2).AddHours(10)));
        Assert.False(list.IsOpen(MondayMidnight.AddDays(3).AddHours(10)));

        var spacedRange = OpenHours.Parse("Mo - Fr 08:00-17:00");
        Assert.True(spacedRange.IsOpen(MondayMidnight.AddHours(10)));
        Assert.False(spacedRange.IsOpen(MondayMidnight.AddDays(5).AddHours(10)));

        var combined = OpenHours.Parse("Mo-We, Fr 08:00-17:00");
        Assert.True(combined.IsOpen(MondayMidnight.AddDays(2).AddHours(10))); // Wed
        Assert.False(combined.IsOpen(MondayMidnight.AddDays(3).AddHours(10))); // Thu
        Assert.True(combined.IsOpen(MondayMidnight.AddDays(4).AddHours(10))); // Fri

        var fullName = OpenHours.Parse("Monday-Friday 08:00-17:00");
        Assert.True(fullName.IsOpen(MondayMidnight.AddHours(10)));
        Assert.False(fullName.IsOpen(MondayMidnight.AddDays(5).AddHours(10)));
    }

    [Fact]
    public void TestWindowsIntegrity()
    {
        var oh = OpenHours.Parse("Mo 08:00-12:00, 13:00-17:00; Tu 08:00-12:00");
        var windows = oh.Windows;
        Assert.Equal(3, windows.Length);
        Assert.Equal(8 * 60, windows[0].Start);
        Assert.Equal(12 * 60, windows[0].End);
        Assert.Equal(13 * 60, windows[1].Start);
        Assert.Equal(17 * 60, windows[1].End);
        Assert.Equal(1440 + 8 * 60, windows[2].Start);
        Assert.Equal(1440 + 12 * 60, windows[2].End);

        // Windows must be sorted and disjoint.
        for (int i = 1; i < windows.Length; i++)
        {
            Assert.True(windows[i].Start >= windows[i - 1].End,
                $"windows not disjoint at {i}: {windows[i - 1]} -> {windows[i]}");
        }
    }

    [Fact]
    public void TestSubMinutePrecision()
    {
        var oh = OpenHours.Parse("Mo 08:00-17:00");
        var expectedEnd = MondayMidnight.AddHours(17);

        // GetCurrentShiftEnd with seconds + sub-second ticks.
        var tOpen = MondayMidnight.AddHours(10).AddMinutes(15).AddSeconds(30).AddTicks(500);
        var shiftEnd = oh.GetCurrentShiftEnd(tOpen);
        Assert.NotNull(shiftEnd);
        Assert.Equal(expectedEnd, shiftEnd);

        // NextDur / NextDate when 30 seconds until close.
        var tNearClose = MondayMidnight.AddHours(16).AddMinutes(59).AddSeconds(30);
        var (open, dur) = oh.NextDur(tNearClose);
        Assert.True(open);
        Assert.Equal(TimeSpan.FromSeconds(30), dur);

        // GetTimeToOpen when 30 seconds until open.
        var tNearOpen = MondayMidnight.AddHours(7).AddMinutes(59).AddSeconds(30);
        var wait = oh.GetTimeToOpen(tNearOpen);
        Assert.NotNull(wait);
        Assert.Equal(TimeSpan.FromSeconds(30), wait);
    }

    [Fact]
    public void TestConcurrentEvaluations()
    {
        var oh = OpenHours.Parse("Mo-Fr 08:00-12:00, 13:00-17:00; Sa 08:00-12:00");
        var baseTime = MondayMidnight;
        const int iterations = 10000;

        Parallel.For(0, 8, g =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var dt = baseTime.AddMinutes((g * 1000 + i) % 10080);
                _ = oh.IsOpen(dt);
                _ = oh.GetTimeToOpen(dt);
            }
        });
    }

    [Fact]
    public void TestJsonConverterRepeatedDeserialization()
    {
        // Exercises the thread-local fast path that caches the last decoded
        // UTF-8 value: ensure correctness across empty, null, and alternating
        // expressions (which force a cache miss + re-decode each time).
        var ctx = OpenHoursJsonContext.Default.OpenHours;

        var a = JsonSerializer.Deserialize("\"Mo-Fr 08:00-17:00\"", ctx);
        Assert.NotNull(a);
        Assert.Equal("Mo-Fr 08:00-17:00", a.Raw);

        // Same value again -> fast-path hit.
        var a2 = JsonSerializer.Deserialize("\"Mo-Fr 08:00-17:00\"", ctx);
        Assert.Same(a, a2);

        // A different expression forces a fresh decode/miss.
        var b = JsonSerializer.Deserialize("\"Sa 09:00-12:00\"", ctx);
        Assert.NotNull(b);
        Assert.Equal("Sa 09:00-12:00", b.Raw);
        Assert.NotSame(a, b);

        // Back to the first -> hit again, returns the same interned instance.
        var a3 = JsonSerializer.Deserialize("\"Mo-Fr 08:00-17:00\"", ctx);
        Assert.Same(a, a3);

        // Empty string.
        var empty = JsonSerializer.Deserialize("\"\"", ctx);
        Assert.NotNull(empty);
        Assert.False(empty.IsOpen(MondayMidnight));

        // Null token.
        var nil = JsonSerializer.Deserialize<OpenHours?>("null", ctx);
        Assert.Null(nil);
    }

    [Fact]
    public void TestRunBenchmarkSuite()
    {
        using var sw = new StringWriter();
        OpenHoursBenchmarks.RunBenchmarks(sw);
        string output = sw.ToString();
        Console.WriteLine(output);
        Assert.Contains("Running OpenHours Benchmarks", output);
        Assert.Contains("Stress Test", output);
    }
}
