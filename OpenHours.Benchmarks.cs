using System.Diagnostics;
using System.Text.Json;

namespace Chneau.OpenHours;

public static class OpenHoursBenchmarks
{
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
        // Multiply iteration counts by this factor so benchmarks run longer and
        // yield more stable per-op timings across all workloads.
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
        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations * 10 * benchScale; i++)
        {
            oh.IsOpen(start.AddTicks(TimeSpan.TicksPerMinute * i));
        }
        sw.Stop();
        long allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc1 = (double)allocDiff / (iterations * 10 * benchScale);
        writer.WriteLine(
            $"1. IsOpen (100k rolling calls):            {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * 10 * benchScale):F3} us/op, {alloc1:F1} B/op)"
        );

        // 2. Benchmark IsOpen (Pure 1M calls with fixed timestamp)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < 1_000_000 * benchScale; i++)
        {
            oh.IsOpen(fixedTime);
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc2 = (double)allocDiff / (1_000_000 * benchScale);
        writer.WriteLine(
            $"2. IsOpen (1M pure calls):                 {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (1_000_000 * benchScale):F3} us/op, {alloc2:F1} B/op)"
        );

        // 3. Benchmark GetTimeToOpen (10k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < iterations * benchScale; i++)
        {
            oh.GetTimeToOpen(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc3 = (double)allocDiff / (iterations * benchScale);
        writer.WriteLine(
            $"3. GetTimeToOpen (10k calls):              {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * benchScale):F3} us/op, {alloc3:F1} B/op)"
        );

        // 4. Benchmark GetTimeToOpenForDuration 4h (10k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < iterations * benchScale; i++)
        {
            oh.GetTimeToOpenForDuration(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours);
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc4 = (double)allocDiff / (iterations * benchScale);
        writer.WriteLine(
            $"4. GetTimeToOpenForDuration 4h (10k calls):{sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * benchScale):F3} us/op, {alloc4:F1} B/op)"
        );

        // 5. Benchmark When 4h (10k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < iterations * benchScale; i++)
        {
            oh.When(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours);
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc5 = (double)allocDiff / (iterations * benchScale);
        writer.WriteLine(
            $"5. When 4h (10k calls):                    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * benchScale):F3} us/op, {alloc5:F1} B/op)"
        );

        // 6. Benchmark NextDur (10k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < iterations * benchScale; i++)
        {
            oh.NextDur(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc6 = (double)allocDiff / (iterations * benchScale);
        writer.WriteLine(
            $"6. NextDur (10k calls):                    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * benchScale):F3} us/op, {alloc6:F1} B/op)"
        );

        // 7. Benchmark NextDate (10k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < iterations * benchScale; i++)
        {
            oh.NextDate(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc7 = (double)allocDiff / (iterations * benchScale);
        writer.WriteLine(
            $"7. NextDate (10k calls):                   {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * benchScale):F3} us/op, {alloc7:F1} B/op)"
        );

        // 8. Benchmark Parse (Cached 1k calls)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < 1000 * benchScale; i++)
        {
            OpenHours.Parse(complexExpr);
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc8 = (double)allocDiff / (1000 * benchScale);
        writer.WriteLine(
            $"8. Parse Cached (1k calls):                {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (1000 * benchScale):F3} us/op, {alloc8:F1} B/op)"
        );

        // 9. Benchmark JSON Deserialization (1k calls)
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes($"\"{complexExpr}\"");
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < 1000 * benchScale; i++)
        {
            OpenHours.DecodeJson(jsonBytes);
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc9 = (double)allocDiff / (1000 * benchScale);
        writer.WriteLine(
            $"9. JSON Deserialize (1k calls):            {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (1000 * benchScale):F3} us/op, {alloc9:F1} B/op)"
        );

        // 10. Simulation Stress Test (5,000 unique locations)
        allocStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        int stressCount = 5000 * benchScale;
        var locations = new List<OpenHours>(stressCount);
        for (int i = 0; i < stressCount; i++)
        {
            int hStart = 8 + (i % 60) / 60;
            int mStart = i % 60;
            int hEnd = 17 + (i % 60) / 60;
            int mEnd = i % 60;
            string expr = $"Mo-Fr {hStart:00}:{mStart:00}-{hEnd:00}:{mEnd:00}";
            locations.Add(OpenHours.Parse(expr));
        }
        sw.Stop();
        allocDiff = GC.GetAllocatedBytesForCurrentThread() - allocStart;
        double alloc10 = (double)allocDiff / stressCount;
        writer.WriteLine(
            $"10. Stress Test (5,000 unique objects):    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMilliseconds / stressCount:F4} ms/obj, {alloc10:F1} B/obj)"
        );
        writer.WriteLine("========================================================");
    }
}
