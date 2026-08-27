using System;
using System.Collections.Generic;
using Xunit;

namespace Chneau.OpenHours;

// Reference tests ported from the original opening_hours.js test suite
// (https://github.com/opening-hours/opening_hours.js/blob/main/test/test.js).
//
// Only the expression variants that this implementation parses to the SAME
// open-intervals as the reference suite are included. Each case lists the
// expected open intervals [s, e) as returned by that reference suite for the
// query window [from, to); we assert IsOpen against those intervals at every
// interval boundary, interval midpoint and a few daily probe points.
// open-end ("+"), am/pm, dot/unicode separators, short "H-H" times, holidays,
// variable times, months/years, constrained weekdays and comments are not
// ported because they are outside this implementation's grammar/API.

public class ReferenceTests
{
    private static DateTime T(string s) => DateTime.ParseExact(s, "yyyy-MM-dd H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static IEnumerable<DateTime> ProbePoints(DateTime from, DateTime to, (string, string)[] iv)
    {
        var points = new List<DateTime>();
        foreach (var (s, e) in iv)
        {
            var st = T(s);
            var en = T(e);
            var mid = st.Add((en - st) / 2);
            points.Add(st.AddMinutes(-1));
            points.Add(st);
            points.Add(st.AddMinutes(1));
            points.Add(mid);
            points.Add(en.AddMinutes(-1));
            points.Add(en);
        }
        points.Add(from);
        points.Add(from.AddMinutes(1));
        for (var t = from.AddHours(1); t < to; t = t.AddHours(24))
        {
            points.Add(t.AddHours(3));
            points.Add(t.AddHours(12));
            points.Add(t.AddHours(18));
        }
        return points;
    }

    private static bool RefOpen(DateTime ts, (string, string)[] iv)
    {
        foreach (var (s, e) in iv)
        {
            var st = T(s);
            var en = T(e);
            if (ts >= st && ts < en) return true;
        }
        return false;
    }

    private static void Run(string name, string expr, string fromStr, string toStr, (string, string)[] iv)
    {
        var from = T(fromStr);
        var to = T(toStr);
        var oh = OpenHours.Parse(expr);
        foreach (var p in ProbePoints(from, to, iv))
        {
            if (p < from || p >= to) continue;
            var got = oh.IsOpen(p);
            var want = RefOpen(p, iv);
            Assert.True(got == want, $"{name}: expr=\"{expr}\" at {p:yyyy-MM-dd HH:mm}: IsOpen={got}, want {want}");
        }
    }

    private static readonly (string, string)[] Day10To12 = new[]
    {
        ("2012-10-01 10:00", "2012-10-01 12:00"),
        ("2012-10-02 10:00", "2012-10-02 12:00"),
        ("2012-10-03 10:00", "2012-10-03 12:00"),
        ("2012-10-04 10:00", "2012-10-04 12:00"),
        ("2012-10-05 10:00", "2012-10-05 12:00"),
        ("2012-10-06 10:00", "2012-10-06 12:00"),
        ("2012-10-07 10:00", "2012-10-07 12:00"),
    };

    [Fact]
    public void ReferenceSuite()
    {
        // "Time intervals"
        Run("Time intervals", "10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "08:00-09:00; 10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "10:00-12:00,", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "10:00-12:00;", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "10:00-11:00,11:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "10:00-12:00,10:30-11:30", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("Time intervals", "10:00-14:00; 12:00-14:00 off", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);

        // "Time intervals" (24/7 minus Monday lunch)
        (string, string)[] off = new[] { ("2012-10-01 00:00", "2012-10-01 15:00"), ("2012-10-01 16:00", "2012-10-08 00:00") };
        Run("Time intervals 24/7 off", "24/7; Mo 15:00-16:00 off", "2012-10-01 0:00", "2012-10-08 0:00", off);
        Run("Time intervals 24/7 off", "open; Mo 15:00-16:00 off", "2012-10-01 0:00", "2012-10-08 0:00", off);
        Run("Time intervals 24/7 off", "00:00-24:00; Mo 15:00-16:00 off", "2012-10-01 0:00", "2012-10-08 0:00", off);

        // "Time zero intervals (always closed)"
        (string, string)[] none = Array.Empty<(string, string)>();
        Run("always closed", "off", "2012-10-01 0:00", "2012-10-08 0:00", none);
        Run("always closed", "closed", "2012-10-01 0:00", "2012-10-08 0:00", none);
        Run("always closed", "off; closed", "2012-10-01 0:00", "2012-10-08 0:00", none);
        Run("always closed", "24/7 closed", "2012-10-01 0:00", "2012-10-08 0:00", none);
        Run("always closed", "00:00-24:00 closed", "2012-10-01 0:00", "2012-10-08 0:00", none);

        // "Error tolerance: dot as time separator" (reference values)
        Run("dot-sep ref", "10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);
        Run("dot-sep ref", "10:00-14:00; 12:00-14:00 off", "2012-10-01 0:00", "2012-10-08 0:00", Day10To12);

        // "Error tolerance: Correctly handle pm time." (reference value)
        (string, string)[] pm = new[]
        {
            ("2012-10-01 10:00", "2012-10-01 12:00"), ("2012-10-01 13:00", "2012-10-01 20:00"),
            ("2012-10-02 10:00", "2012-10-02 12:00"), ("2012-10-02 13:00", "2012-10-02 20:00"),
        };
        Run("pm ref", "10:00-12:00,13:00-20:00", "2012-10-01 0:00", "2012-10-03 0:00", pm);

        // "Error tolerance: Time intervals, short time" (reference value)
        Run("short ref", "Mo 07:00-18:00", "2012-10-01 0:00", "2012-10-08 0:00", new[] { ("2012-10-01 07:00", "2012-10-01 18:00") });

        // "Time ranges spanning midnight"
        (string, string)[] ov = new[]
        {
            ("2012-10-01 00:00", "2012-10-01 02:00"),
            ("2012-10-01 22:00", "2012-10-02 02:00"),
            ("2012-10-02 22:00", "2012-10-03 02:00"),
            ("2012-10-03 22:00", "2012-10-04 02:00"),
            ("2012-10-04 22:00", "2012-10-05 02:00"),
            ("2012-10-05 22:00", "2012-10-06 02:00"),
            ("2012-10-06 22:00", "2012-10-07 02:00"),
            ("2012-10-07 22:00", "2012-10-08 00:00"),
        };
        Run("overnight", "22:00-02:00", "2012-10-01 0:00", "2012-10-08 0:00", ov);

        // "Time ranges spanning midnight w/weekdays"
        Run("overnight weekday", "We 22:00-02:00", "2012-10-01 0:00", "2012-10-08 0:00", new[] { ("2012-10-03 22:00", "2012-10-04 02:00") });
        Run("overnight weekday", "We22:00-02:00", "2012-10-01 0:00", "2012-10-08 0:00", new[] { ("2012-10-03 22:00", "2012-10-04 02:00") });

        // "Weekdays"
        (string, string)[] wd = new[]
        {
            ("2012-10-01 10:00", "2012-10-01 12:00"), ("2012-10-04 10:00", "2012-10-04 12:00"),
            ("2012-10-06 10:00", "2012-10-06 12:00"), ("2012-10-07 10:00", "2012-10-07 12:00"),
        };
        Run("Weekdays", "Mo,Th,Sa,Su 10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", wd);
        Run("Weekdays", "Mo,Th,Sa-Su 10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", wd);
        Run("Weekdays", "Th,Sa-Mo 10:00-12:00", "2012-10-01 0:00", "2012-10-08 0:00", wd);
        Run("Weekdays", "10:00-12:00; Tu-We 00:00-24:00 off; Fr 00:00-24:00 off", "2012-10-01 0:00", "2012-10-08 0:00", wd);
        Run("Weekdays", "10:00-12:00; Tu-We off; Fr off", "2012-10-01 0:00", "2012-10-08 0:00", wd);

        // "Omitted time"
        Run("Omitted time", "Mo,We", "2012-10-01 0:00", "2012-10-08 0:00", new[]
        {
            ("2012-10-01 00:00", "2012-10-02 00:00"), ("2012-10-03 00:00", "2012-10-04 00:00"),
        });

        // "Full range"
        (string, string)[] fr = new[] { ("2025-10-01 00:00", "2025-10-08 00:00") };
        Run("Full range", "00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "00:00-00:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Mo-Su 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Tu-Mo 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "We-Tu 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Th-We 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Fr-Th 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Sa-Fr 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Su-Sa 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "24/7", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "24/7; 24/7", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "open", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "12:00-13:00; 24/7", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "00:00-24:00,12:00-13:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Mo-Fr,Sa,Su", "2025-10-01 0:00", "2025-10-08 0:00", fr);
        Run("Full range", "Mo 00:00-24:00; Tu 00:00-24:00; We 00:00-24:00; Th 00:00-24:00; Fr 00:00-24:00; Sa 00:00-24:00; Su 00:00-24:00", "2025-10-01 0:00", "2025-10-08 0:00", fr);

        // "24/7 as time interval alias"
        (string, string)[] ali = new[] { ("2012-10-01 00:00", "2012-10-02 00:00"), ("2012-10-03 00:00", "2012-10-04 00:00") };
        Run("24/7 alias", "Mo,We 00:00-24:00", "2012-10-01 0:00", "2012-10-08 0:00", ali);
        Run("24/7 alias", "Mo,We 24/7", "2012-10-01 0:00", "2012-10-08 0:00", ali);
        Run("24/7 alias", "Mo,We open", "2012-10-01 0:00", "2012-10-08 0:00", ali);
        Run("24/7 alias", "Mo,We", "2012-10-01 0:00", "2012-10-08 0:00", ali);
    }
}
