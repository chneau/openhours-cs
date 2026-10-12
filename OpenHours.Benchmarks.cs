using System.Diagnostics;
using System.Text.Json;

namespace Chneau.OpenHours;

public static class OpenHoursBenchmarks
{
    private const double TargetMs = 500.0;

    // Runs op(0..baseCount-1) repeatedly until the block runs for at least
    // TargetMs, then takes one measured pass. Returns the measured ms, the
    // bytes allocated during that pass, and the total op count, so per-op
    // numbers are not dominated by clock granularity.
    private static (double ms, long alloc, long total) Measure(int baseCount, Action<int> op)
    {
        long reps = 1;
        while (true)
        {
            var sw0 = Stopwatch.StartNew();
            for (long r = 0; r < reps; r++)
            {
                for (int i = 0; i < baseCount; i++)
                {
                    op(i);
                }
            }
            sw0.Stop();
            double el = sw0.Elapsed.TotalMilliseconds;
            if (el >= TargetMs || reps > (1L << 34) / baseCount)
            {
                break;
            }
            long next = (long)(reps * TargetMs / el) + 1;
            if (next <= reps)
            {
                next = reps + 1;
            }
            reps = next;
        }

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        long total = 0;
        while (true)
        {
            for (long r = 0; r < reps; r++)
            {
                for (int i = 0; i < baseCount; i++)
                {
                    op(i);
                }
            }
            total += reps * baseCount;
            if (sw.Elapsed.TotalMilliseconds >= TargetMs)
            {
                break;
            }
        }
        sw.Stop();
        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        return (sw.Elapsed.TotalMilliseconds, allocAfter - allocBefore, total);
    }

    public static void RunBenchmarks(TextWriter? writer = null)
    {
        writer ??= Console.Out;
        writer.WriteLine("========================================================");
        writer.WriteLine("Running OpenHours Benchmarks (Full Standard Suite)");
        writer.WriteLine("========================================================");

        string complexExpr = "Mo-Fr 08:00-12:00, 13:00-17:00; Sa 08:00-12:00";
        var oh = OpenHours.Parse(complexExpr);
        var start = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc); // Monday midnight
        var fixedTime = new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);
        int iterations = 10000;
        var fourHours = TimeSpan.FromHours(4);
        // Base workload sizes; each step is repeated until it runs for TargetMs.
        const int benchScale = 10;

        // Warm-up JIT Tier 0 / Tier 1 compilation
        byte[] warmupJson = System.Text.Encoding.UTF8.GetBytes($"\"{complexExpr}\"");
        for (int i = 0; i < 10000; i++)
        {
            oh.IsOpen(start.AddTicks(TimeSpan.TicksPerMinute * (i % 168)));
            oh.GetTimeToOpen(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
            OpenHours.Parse(complexExpr);
            OpenHours.DecodeJson(warmupJson);
            JsonSerializer.Deserialize(warmupJson, OpenHoursJsonContext.Default.OpenHours);
        }

        // 1. Benchmark IsOpen (Rolling 100k calls with timestamp addition)
        var (ms1, alloc1, total1) = Measure(iterations * 10 * benchScale,
            i => oh.IsOpen(start.AddTicks(TimeSpan.TicksPerMinute * i)));
        writer.WriteLine(
            $"1. IsOpen (100k rolling calls):            {ms1,4:F0} ms ({ms1 * 1000.0 / total1:F5} us/op, {(double)alloc1 / total1:F1} B/op)"
        );

        // 2. Benchmark IsOpen (Pure 1M calls with fixed timestamp)
        var (ms2, alloc2, total2) = Measure(1_000_000 * benchScale,
            i => oh.IsOpen(fixedTime));
        writer.WriteLine(
            $"2. IsOpen (1M pure calls):                 {ms2,4:F0} ms ({ms2 * 1000.0 / total2:F5} us/op, {(double)alloc2 / total2:F1} B/op)"
        );

        // 3. Benchmark GetTimeToOpen (10k calls)
        var (ms3, alloc3, total3) = Measure(iterations * benchScale,
            i => oh.GetTimeToOpen(start.AddTicks(TimeSpan.TicksPerHour * (i % 168))));
        writer.WriteLine(
            $"3. GetTimeToOpen (10k calls):              {ms3,4:F0} ms ({ms3 * 1000.0 / total3:F5} us/op, {(double)alloc3 / total3:F1} B/op)"
        );

        // 4. Benchmark GetTimeToOpenForDuration 4h (10k calls)
        var (ms4, alloc4, total4) = Measure(iterations * benchScale,
            i => oh.GetTimeToOpenForDuration(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours));
        writer.WriteLine(
            $"4. GetTimeToOpenForDuration 4h (10k calls):{ms4,4:F0} ms ({ms4 * 1000.0 / total4:F5} us/op, {(double)alloc4 / total4:F1} B/op)"
        );

        // 5. Benchmark When 4h (10k calls)
        var (ms5, alloc5, total5) = Measure(iterations * benchScale,
            i => oh.When(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours));
        writer.WriteLine(
            $"5. When 4h (10k calls):                    {ms5,4:F0} ms ({ms5 * 1000.0 / total5:F5} us/op, {(double)alloc5 / total5:F1} B/op)"
        );

        // 6. Benchmark NextDur (10k calls)
        var (ms6, alloc6, total6) = Measure(iterations * benchScale,
            i => oh.NextDur(start.AddTicks(TimeSpan.TicksPerHour * (i % 168))));
        writer.WriteLine(
            $"6. NextDur (10k calls):                    {ms6,4:F0} ms ({ms6 * 1000.0 / total6:F5} us/op, {(double)alloc6 / total6:F1} B/op)"
        );

        // 7. Benchmark NextDate (10k calls)
        var (ms7, alloc7, total7) = Measure(iterations * benchScale,
            i => oh.NextDate(start.AddTicks(TimeSpan.TicksPerHour * (i % 168))));
        writer.WriteLine(
            $"7. NextDate (10k calls):                   {ms7,4:F0} ms ({ms7 * 1000.0 / total7:F5} us/op, {(double)alloc7 / total7:F1} B/op)"
        );

        // 8. Benchmark Parse (Cached 1k calls)
        var (ms8, alloc8, total8) = Measure(1000 * benchScale,
            i => OpenHours.Parse(complexExpr));
        writer.WriteLine(
            $"8. Parse Cached (1k calls):                {ms8,4:F0} ms ({ms8 * 1000.0 / total8:F5} us/op, {(double)alloc8 / total8:F1} B/op)"
        );

        // 9. Benchmark JSON Deserialization (1k calls)
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes($"\"{complexExpr}\"");
        var (ms9, alloc9, total9) = Measure(1000 * benchScale,
            i => OpenHours.DecodeJson(jsonBytes));
        writer.WriteLine(
            $"9. JSON Deserialize (1k calls):            {ms9,4:F0} ms ({ms9 * 1000.0 / total9:F5} us/op, {(double)alloc9 / total9:F1} B/op)"
        );

        // 10. Simulation Stress Test (unique locations)
        var (ms10, alloc10, total10) = Measure(5000 * benchScale, i =>
        {
            int hStart = 8 + (i % 60) / 60;
            int mStart = i % 60;
            int hEnd = 17 + (i % 60) / 60;
            int mEnd = i % 60;
            string expr = $"Mo-Fr {hStart:00}:{mStart:00}-{hEnd:00}:{mEnd:00}";
            OpenHours.Parse(expr);
        });
        writer.WriteLine(
            $"10. Stress Test (5,000 unique objects):    {ms10,4:F0} ms ({ms10 / total10:F4} ms/obj, {(double)alloc10 / total10:F1} B/obj)"
        );
        writer.WriteLine("========================================================");
    }
}
