using System.Diagnostics;
using System.Text.Json;

namespace Chneau.Time;

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

        // Warm-up JIT Tier 0 / Tier 1 compilation
        byte[] warmupJson = System.Text.Encoding.UTF8.GetBytes($"\"{complexExpr}\"");
        for (int i = 0; i < 10000; i++)
        {
            oh.IsOpen(start.AddTicks(TimeSpan.TicksPerMinute * (i % 168)));
            oh.GetTimeToOpen(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
            OpenHours.Parse(complexExpr);
            JsonSerializer.Deserialize(warmupJson, OpenHoursJsonContext.Default.OpenHours);
        }

        // 1. Benchmark IsOpen (Rolling 100k calls with timestamp addition)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations * 10; i++)
        {
            oh.IsOpen(start.AddTicks(TimeSpan.TicksPerMinute * i));
        }
        sw.Stop();
        writer.WriteLine(
            $"1. IsOpen (100k rolling calls):            {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / (iterations * 10):F3} us/op)"
        );

        // 2. Benchmark IsOpen (Pure 1M calls with fixed timestamp)
        sw.Restart();
        for (int i = 0; i < 1_000_000; i++)
        {
            oh.IsOpen(fixedTime);
        }
        sw.Stop();
        writer.WriteLine(
            $"2. IsOpen (1M pure calls):                 {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / 1_000_000.0:F3} us/op)"
        );

        // 3. Benchmark GetTimeToOpen (10k calls)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            oh.GetTimeToOpen(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        writer.WriteLine(
            $"3. GetTimeToOpen (10k calls):              {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / iterations:F3} us/op)"
        );

        // 4. Benchmark GetTimeToOpenForDuration 4h (10k calls)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            oh.GetTimeToOpenForDuration(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours);
        }
        sw.Stop();
        writer.WriteLine(
            $"4. GetTimeToOpenForDuration 4h (10k calls):{sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / iterations:F3} us/op)"
        );

        // 5. Benchmark When 4h (10k calls)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            oh.When(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)), fourHours);
        }
        sw.Stop();
        writer.WriteLine(
            $"5. When 4h (10k calls):                    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / iterations:F3} us/op)"
        );

        // 6. Benchmark NextDur (10k calls)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            oh.NextDur(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        writer.WriteLine(
            $"6. NextDur (10k calls):                    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / iterations:F3} us/op)"
        );

        // 7. Benchmark NextDate (10k calls)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            oh.NextDate(start.AddTicks(TimeSpan.TicksPerHour * (i % 168)));
        }
        sw.Stop();
        writer.WriteLine(
            $"7. NextDate (10k calls):                   {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / iterations:F3} us/op)"
        );

        // 8. Benchmark Parse (Cached 1k calls)
        sw.Restart();
        for (int i = 0; i < 1000; i++)
        {
            OpenHours.Parse(complexExpr);
        }
        sw.Stop();
        writer.WriteLine(
            $"8. Parse Cached (1k calls):                {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / 1000.0:F3} us/op)"
        );

        // 9. Benchmark JSON Deserialization (1k calls)
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes($"\"{complexExpr}\"");
        sw.Restart();
        for (int i = 0; i < 1000; i++)
        {
            JsonSerializer.Deserialize(jsonBytes, OpenHoursJsonContext.Default.OpenHours);
        }
        sw.Stop();
        writer.WriteLine(
            $"9. JSON Deserialize (1k calls):            {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMicroseconds / 1000.0:F3} us/op)"
        );

        // 10. Simulation Stress Test (5,000 unique locations)
        sw.Restart();
        var locations = new List<OpenHours>(5000);
        for (int i = 0; i < 5000; i++)
        {
            int hStart = 8 + (i % 60) / 60;
            int mStart = i % 60;
            int hEnd = 17 + (i % 60) / 60;
            int mEnd = i % 60;
            string expr = $"Mo-Fr {hStart:00}:{mStart:00}-{hEnd:00}:{mEnd:00}";
            locations.Add(OpenHours.Parse(expr));
        }
        sw.Stop();
        writer.WriteLine(
            $"10. Stress Test (5,000 unique objects):    {sw.ElapsedMilliseconds,4} ms ({sw.Elapsed.TotalMilliseconds / 5000.0:F4} ms/obj)"
        );
        writer.WriteLine("========================================================");
    }
}
