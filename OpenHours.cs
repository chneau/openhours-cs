using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chneau.OpenHours;

[InlineArray(158)]
public struct Bitmask158
{
    private ulong _element0;
}

/// <summary>
/// A high-performance, zero-allocation parser and interval-math evaluator for OpenStreetMap opening_hours.
/// </summary>
[JsonConverter(typeof(OpenHoursJsonConverter))]
public sealed class OpenHours
{
    private static readonly Lock _poolLock = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, OpenHours> _internPool = new(System.Environment.ProcessorCount * 4, 256, StringComparer.Ordinal);
    private const int MinutesPerWeek = 10080;

    private static readonly OpenHours _empty = new("", [], default);
    private static readonly OpenHours _alwaysOpen = CreateAlwaysOpen();

    private static OpenHours CreateAlwaysOpen()
    {
        var oh = new OpenHours("24/7", [new TimeWindow(0, MinutesPerWeek)], default);
        for (int i = 0; i < 158; i++)
        {
            oh._bitmask[i] = ~0UL;
        }
        return oh;
    }

    private readonly string _expression;
    private readonly TimeWindow[] _windows;
    private Bitmask158 _bitmask;

    public string Raw => _expression;
    public TimeWindow[] Windows => (TimeWindow[])_windows.Clone();

    private OpenHours(string expression, TimeWindow[] windows, Bitmask158 bitmask)
    {
        _expression = expression;
        _windows = windows;
        _bitmask = bitmask;
    }

    // A single thread-local holder bundles the "last UTF-8 JSON payload seen on
    // this thread" memo (raw bytes + length + decoded instance) so a repeated
    // payload costs one ThreadStatic read instead of three separate ones.
    private sealed class JsonCache
    {
        public byte[] Bytes = [];
        public int Len;
        public OpenHours? Val;
    }

    // The string-keyed parse memo. Bundling the key and value into one
    // thread-local struct keeps the hot lookup down to a single ThreadStatic
    // read (one TLS access, then register compares).
    private struct FastEntry
    {
        public string? Key;
        public OpenHours? Val;
    }

    [ThreadStatic]
    private static FastEntry _fastEntry;
    [ThreadStatic]
    private static JsonCache? _fastJson;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpenHours From(string? expression) => Parse(expression);

    // Fast path kept deliberately tiny so the JIT can inline the common
    // "same expression as last time" case straight into callers (e.g. a tight
    // parse loop). All of the cold work lives in ParseStringSlow, which the JIT
    // is told not to inline.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpenHours Parse(string? expression)
    {
        if (expression is null || expression.Length == 0)
        {
            return _empty;
        }

        var e = _fastEntry;
        if (ReferenceEquals(e.Key, expression) || e.Key == expression)
        {
            return e.Val!;
        }

        return ParseStringSlow(expression);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OpenHours ParseStringSlow(string expression)
    {
        if (ReferenceEquals(expression, "24/7") || expression == "24/7")
        {
            return _alwaysOpen;
        }

        if (_internPool.TryGetValue(expression, out var cached))
        {
            _fastEntry = new FastEntry { Key = expression, Val = cached };
            return cached;
        }

        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return _empty;
        }

        OpenHours result;
        if (trimmed == "24/7")
        {
            result = _alwaysOpen;
        }
        else
        {
            result = ParseUncached(expression, trimmed);
        }

        lock (_poolLock)
        {
            if (_internPool.TryGetValue(expression, out var existing))
            {
                _fastEntry = new FastEntry { Key = expression, Val = existing };
                return existing;
            }
        }
        _fastEntry = new FastEntry { Key = expression, Val = result };
        _internPool.TryAdd(expression, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpenHours Parse(ReadOnlySpan<char> expressionSpan)
    {
        if (expressionSpan.IsEmpty)
        {
            return _empty;
        }

        if (expressionSpan.Equals("24/7", StringComparison.Ordinal))
        {
            return _alwaysOpen;
        }

        if (_internPool.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(expressionSpan, out var cached))
        {
            return cached;
        }

        var trimmed = expressionSpan.Trim();
        if (trimmed.IsEmpty)
        {
            return _empty;
        }

        if (trimmed.Equals("24/7", StringComparison.Ordinal))
        {
            return _alwaysOpen;
        }

        if (_internPool.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(trimmed, out var cachedTrimmed))
        {
            return cachedTrimmed;
        }

        string rawString = expressionSpan.ToString();
        var result = ParseUncached(rawString, rawString.Trim());
        if (_internPool.TryGetValue(rawString, out var existing))
        {
            return existing;
        }
        _internPool.TryAdd(rawString, result);
        return result;
    }

    // The UTF-8 decode memo (last payload bytes + decoded instance) lives in the
    // same thread-local FastCache holder as the parse memo so a repeated payload
    // costs a single ThreadStatic read plus one vectorised span compare, skipping
    // the escape scan, the UTF-8 -> UTF-16 transcode and the intern-pool lookup.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpenHours? DecodeJson(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length >= 2 && utf8Json[0] == (byte)'"' && utf8Json[^1] == (byte)'"')
        {
            var inner = utf8Json[1..^1];
            int len = inner.Length;
            if (len == 0)
            {
                return _empty;
            }

            var f = _fastJson;
            if (f is not null && len == f.Len && f.Val is { } cached
                && inner.SequenceEqual(f.Bytes.AsSpan(0, len)))
            {
                return cached;
            }

            if (!HasJsonEscape(inner))
            {
                Span<char> chars = stackalloc char[len];
                for (int i = 0; i < len; i++)
                {
                    chars[i] = (char)inner[i];
                }
                var parsed = Parse(chars);
                _fastEntry = new FastEntry { Key = parsed.Raw, Val = parsed };
                var fc = _fastJson ??= new JsonCache();
                fc.Bytes = inner.ToArray();
                fc.Len = len;
                fc.Val = parsed;
                return parsed;
            }
        }

        return DecodeJsonSlow(utf8Json);
    }

    private static bool HasJsonEscape(ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b == (byte)'\\' || b < 0x20)
            {
                return true;
            }
        }
        return false;
    }

    private static OpenHours? DecodeJsonSlow(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        if (!reader.Read()) return null;
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String) return null;
        var s = reader.GetString();
        return s is not null ? Parse(s) : null;
    }

    private static OpenHours ParseUncached(string rawExpression, string trimmed)
    {
        Span<OpeningRule> rules = stackalloc OpeningRule[8];
        int ruleCount = 0;

        var remaining = trimmed.AsSpan();
        while (remaining.Length > 0)
        {
            int idx = remaining.IndexOf(';');
            ReadOnlySpan<char> part;
            if (idx >= 0)
            {
                part = remaining[..idx].Trim();
                remaining = remaining[(idx + 1)..];
            }
            else
            {
                part = remaining.Trim();
                remaining = default;
            }

            if (part.Length > 0)
            {
                var rule = ParseRule(part);
                if (rule.DayMask > 0 && (rule.IsAllDay || rule.NumRanges > 0))
                {
                    if (ruleCount < rules.Length)
                    {
                        rules[ruleCount++] = rule;
                    }
                }
            }
        }

        var baked = Bake(rules[..ruleCount]);
        var oh = new OpenHours(rawExpression, baked, default);
        BakeBitmask(baked, ref oh._bitmask);
        return oh;
    }

    private static void BakeBitmask(TimeWindow[] windows, ref Bitmask158 bm)
    {
        foreach (var w in windows)
        {
            int start = w.Start < 0 ? 0 : w.Start;
            int end = w.End > MinutesPerWeek ? MinutesPerWeek : w.End;
            if (start >= end) continue;

            int startWord = start >> 6;
            int endWord = (end - 1) >> 6;
            int startBit = start & 63;
            int endBit = (end - 1) & 63;

            if (startWord == endWord)
            {
                ulong mask = ((1UL << (endBit - startBit + 1)) - 1UL) << startBit;
                bm[startWord] |= mask;
            }
            else
            {
                bm[startWord] |= (~0UL) << startBit;
                for (int i = startWord + 1; i < endWord; i++)
                {
                    bm[i] = ~0UL;
                }
                bm[endWord] |= (~0UL) >> (63 - endBit);
            }
        }
    }

    private static OpeningRule ParseRule(ReadOnlySpan<char> ruleSpan)
    {
        var rule = new OpeningRule();
        var span = ruleSpan.Trim();

        bool isOff = false;
        if (span.EndsWith("off", StringComparison.OrdinalIgnoreCase))
        {
            isOff = true;
            span = span[..^3].Trim();
        }
        else if (span.EndsWith("closed", StringComparison.OrdinalIgnoreCase))
        {
            isOff = true;
            span = span[..^6].Trim();
        }
        else if (span.EndsWith("open", StringComparison.OrdinalIgnoreCase))
        {
            span = span[..^4].Trim();
        }

        rule.IsOff = isOff;

        if (span.IsEmpty)
        {
            if (!isOff)
            {
                rule.DayMask = 0b1111111;
                rule.IsAllDay = true;
            }
            return rule;
        }

        int firstDigitOrPlus = -1;
        for (int i = 0; i < span.Length; i++)
        {
            if (char.IsAsciiDigit(span[i]) || span[i] == ':')
            {
                firstDigitOrPlus = i;
                break;
            }
        }

        ReadOnlySpan<char> dayPart;
        ReadOnlySpan<char> timePart;

        if (firstDigitOrPlus == -1)
        {
            dayPart = span;
            timePart = default;
        }
        else if (firstDigitOrPlus == 0)
        {
            dayPart = default;
            timePart = span;
        }
        else
        {
            dayPart = span[..firstDigitOrPlus].Trim();
            timePart = span[firstDigitOrPlus..].Trim();
        }

        if (dayPart.Length > 0)
        {
            rule.DayMask = ParseDayMask(dayPart);
        }
        else
        {
            rule.DayMask = 0b1111111;
        }

        if (timePart.Length > 0)
        {
            ParseTimes(timePart, ref rule);
            rule.IsAllDay = false;
        }
        else
        {
            rule.IsAllDay = true;
        }

        return rule;
    }

    private static int ParseDayMask(ReadOnlySpan<char> dayPart)
    {
        int mask = 0;
        var remaining = dayPart;
        while (remaining.Length > 0)
        {
            int idx = remaining.IndexOf(',');
            ReadOnlySpan<char> group;
            if (idx >= 0)
            {
                group = remaining[..idx].Trim();
                remaining = remaining[(idx + 1)..];
            }
            else
            {
                group = remaining.Trim();
                remaining = default;
            }

            if (group.Length == 0)
            {
                continue;
            }

            int dashIdx = group.IndexOf('-');
            if (dashIdx >= 0)
            {
                var p1 = group[..dashIdx].Trim();
                var p2 = group[(dashIdx + 1)..].Trim();
                int start = DayToIndex(p1);
                int end = DayToIndex(p2);
                if (start != -1 && end != -1)
                {
                    for (int curr = start; ; curr = (curr + 1) % 7)
                    {
                        mask |= (1 << curr);
                        if (curr == end)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                int dayIdx = DayToIndex(group);
                if (dayIdx != -1)
                {
                    mask |= (1 << dayIdx);
                }
                else
                {
                    return 0;
                }
            }
        }
        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DayToIndex(ReadOnlySpan<char> s)
    {
        s = s.Trim();
        if (s.Length < 2)
        {
            return -1;
        }
        char c0 = (char)(s[0] | 0x20);
        char c1 = (char)(s[1] | 0x20);
        int pair = (c0 << 8) | c1;

        switch (pair)
        {
            case ('m' << 8) | 'o':
                if (s.Length == 2 || s.Equals("mon", StringComparison.OrdinalIgnoreCase) || s.Equals("monday", StringComparison.OrdinalIgnoreCase)) return 0;
                break;
            case ('t' << 8) | 'u':
                if (s.Length == 2 || s.Equals("tue", StringComparison.OrdinalIgnoreCase) || s.Equals("tues", StringComparison.OrdinalIgnoreCase) || s.Equals("tuesday", StringComparison.OrdinalIgnoreCase)) return 1;
                break;
            case ('w' << 8) | 'e':
                if (s.Length == 2 || s.Equals("wed", StringComparison.OrdinalIgnoreCase) || s.Equals("wednesday", StringComparison.OrdinalIgnoreCase)) return 2;
                break;
            case ('t' << 8) | 'h':
                if (s.Length == 2 || s.Equals("thu", StringComparison.OrdinalIgnoreCase) || s.Equals("thur", StringComparison.OrdinalIgnoreCase) || s.Equals("thurs", StringComparison.OrdinalIgnoreCase) || s.Equals("thursday", StringComparison.OrdinalIgnoreCase)) return 3;
                break;
            case ('f' << 8) | 'r':
                if (s.Length == 2 || s.Equals("fri", StringComparison.OrdinalIgnoreCase) || s.Equals("friday", StringComparison.OrdinalIgnoreCase)) return 4;
                break;
            case ('s' << 8) | 'a':
                if (s.Length == 2 || s.Equals("sat", StringComparison.OrdinalIgnoreCase) || s.Equals("saturday", StringComparison.OrdinalIgnoreCase)) return 5;
                break;
            case ('s' << 8) | 'u':
                if (s.Length == 2 || s.Equals("sun", StringComparison.OrdinalIgnoreCase) || s.Equals("sunday", StringComparison.OrdinalIgnoreCase)) return 6;
                break;
        }
        return -1;
    }

    private static void ParseTimes(ReadOnlySpan<char> timePart, ref OpeningRule rule)
    {
        var remaining = timePart;
        while (remaining.Length > 0)
        {
            int idx = remaining.IndexOf(',');
            ReadOnlySpan<char> group;
            if (idx >= 0)
            {
                group = remaining[..idx].Trim();
                remaining = remaining[(idx + 1)..];
            }
            else
            {
                group = remaining.Trim();
                remaining = default;
            }

            if (group.Length == 0)
            {
                continue;
            }

            if (group.Equals("24/7", StringComparison.Ordinal) || group.Equals("00:00-24:00", StringComparison.Ordinal) || group.Equals("00:00-00:00", StringComparison.Ordinal))
            {
                rule.AddRange(new TimeRange(0, 1440, 0));
            }
            else
            {
                int dashIdx = group.IndexOf('-');
                if (dashIdx >= 0)
                {
                    var p1 = group[..dashIdx].Trim();
                    var p2 = group[(dashIdx + 1)..].Trim();
                    if (TryParseTimeMin(p1, out int s) && TryParseTimeMin(p2, out int e))
                    {
                        if (s == 0 && e == 0)
                        {
                            rule.AddRange(new TimeRange(0, 1440, 0));
                        }
                        else if (s < e)
                        {
                            rule.AddRange(new TimeRange(s, e, 0));
                        }
                        else if (s > e)
                        {
                            rule.AddRange(new TimeRange(s, 1440, 0));
                            rule.AddRange(new TimeRange(0, e, 1));
                        }
                    }
                }
                else if (group.EndsWith('+') && TryParseTimeMin(group[..^1].Trim(), out int sOnly))
                {
                    rule.AddRange(new TimeRange(sOnly, 1440, 0));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseTimeMin(ReadOnlySpan<char> s, out int minutes)
    {
        s = s.Trim();
        if (s.Equals("24:00", StringComparison.Ordinal))
        {
            minutes = 1440;
            return true;
        }

        // Fast path for HH:MM (5 chars)
        if (s.Length == 5 && s[2] == ':')
        {
            uint h0 = (uint)(s[0] - '0');
            uint h1 = (uint)(s[1] - '0');
            uint m0 = (uint)(s[3] - '0');
            uint m1 = (uint)(s[4] - '0');
            if ((h0 | h1 | m0 | m1) < 10)
            {
                int h = (int)(h0 * 10 + h1);
                int m = (int)(m0 * 10 + m1);
                if ((uint)h < 24 && (uint)m < 60)
                {
                    minutes = h * 60 + m;
                    return true;
                }
            }
            minutes = 0;
            return false;
        }

        // Fast path for H:MM (4 chars)
        if (s.Length == 4 && s[1] == ':')
        {
            uint h0 = (uint)(s[0] - '0');
            uint m0 = (uint)(s[2] - '0');
            uint m1 = (uint)(s[3] - '0');
            if ((h0 | m0 | m1) < 10)
            {
                int m = (int)(m0 * 10 + m1);
                if ((uint)m < 60)
                {
                    minutes = (int)h0 * 60 + m;
                    return true;
                }
            }
            minutes = 0;
            return false;
        }

        int colonIdx = s.IndexOf(':');
        if (colonIdx >= 1 && colonIdx < s.Length - 1)
        {
            if (ParseDigits(s[..colonIdx], out int h) && ParseDigits(s[(colonIdx + 1)..], out int m) && h is >= 0 and < 24 && m is >= 0 and < 60)
            {
                minutes = h * 60 + m;
                return true;
            }
        }

        minutes = 0;
        return false;
    }

    private static bool ParseDigits(ReadOnlySpan<char> s, out int val)
    {
        s = s.Trim();
        if (s.Length == 1 && char.IsAsciiDigit(s[0]))
        {
            val = s[0] - '0';
            return true;
        }
        if (s.Length == 2 && char.IsAsciiDigit(s[0]) && char.IsAsciiDigit(s[1]))
        {
            val = (s[0] - '0') * 10 + (s[1] - '0');
            return true;
        }
        val = 0;
        return false;
    }

    private static TimeWindow[] Bake(ReadOnlySpan<OpeningRule> rules)
    {
        Span<TimeWindow> openBuf = stackalloc TimeWindow[32];
        int openCount = 0;

        Span<TimeWindow> ruleBuf = stackalloc TimeWindow[16];

        foreach (ref readonly var rule in rules)
        {
            int ruleCount = 0;
            for (int day = 0; day < 7; day++)
            {
                if ((rule.DayMask & (1 << day)) == 0)
                {
                    continue;
                }

                if (rule.IsAllDay)
                {
                    int dayOffset = day * 1440;
                    ruleBuf[ruleCount++] = new TimeWindow(dayOffset, dayOffset + 1440);
                }
                else
                {
                    for (int k = 0; k < rule.NumRanges; k++)
                    {
                        var r = rule.GetRange(k);
                        int start = (day + r.DayOffset) * 1440 + r.StartMin;
                        int end = (day + r.DayOffset) * 1440 + r.EndMin;

                        if (start < MinutesPerWeek && end <= MinutesPerWeek)
                        {
                            ruleBuf[ruleCount++] = new TimeWindow(start, end);
                        }
                        else if (start < MinutesPerWeek && end > MinutesPerWeek)
                        {
                            ruleBuf[ruleCount++] = new TimeWindow(start, MinutesPerWeek);
                            ruleBuf[ruleCount++] = new TimeWindow(0, end - MinutesPerWeek);
                        }
                        else
                        {
                            ruleBuf[ruleCount++] = new TimeWindow(start - MinutesPerWeek, end - MinutesPerWeek);
                        }
                    }
                }
            }

            if (!rule.IsOff)
            {
                for (int i = 0; i < ruleCount; i++)
                {
                    openBuf[openCount++] = ruleBuf[i];
                }
                openCount = MergeInPlace(openBuf[..openCount]);
            }
            else
            {
                openCount = SubtractInPlace(openBuf, openCount, ruleBuf[..ruleCount]);
            }
        }

        var result = new TimeWindow[openCount];
        openBuf[..openCount].CopyTo(result);
        return result;
    }

    private static int MergeInPlace(Span<TimeWindow> windows)
    {
        if (windows.Length <= 1)
        {
            return windows.Length;
        }

        SortWindows(windows);

        int writeIdx = 0;
        for (int i = 1; i < windows.Length; i++)
        {
            if (windows[i].Start <= windows[writeIdx].End)
            {
                if (windows[i].End > windows[writeIdx].End)
                {
                    windows[writeIdx] = new TimeWindow(windows[writeIdx].Start, windows[i].End);
                }
            }
            else
            {
                writeIdx++;
                windows[writeIdx] = windows[i];
            }
        }
        return writeIdx + 1;
    }

    private static void SortWindows(Span<TimeWindow> windows)
    {
        for (int i = 1; i < windows.Length; i++)
        {
            var key = windows[i];
            int j = i - 1;
            while (j >= 0 && windows[j].Start > key.Start)
            {
                windows[j + 1] = windows[j];
                j--;
            }
            windows[j + 1] = key;
        }
    }

    private static int SubtractInPlace(Span<TimeWindow> source, int sourceCount, Span<TimeWindow> subtrahends)
    {
        int subCount = MergeInPlace(subtrahends);
        var subs = subtrahends[..subCount];

        Span<TimeWindow> nextBuf = stackalloc TimeWindow[32];

        for (int i = 0; i < subs.Length; i++)
        {
            ref readonly var sub = ref subs[i];
            int nextCount = 0;

            for (int j = 0; j < sourceCount; j++)
            {
                ref readonly var s = ref source[j];
                if (sub.Start >= s.End || sub.End <= s.Start)
                {
                    nextBuf[nextCount++] = s;
                }
                else
                {
                    if (sub.Start > s.Start)
                    {
                        nextBuf[nextCount++] = new TimeWindow(s.Start, sub.Start);
                    }
                    if (sub.End < s.End)
                    {
                        nextBuf[nextCount++] = new TimeWindow(sub.End, s.End);
                    }
                }
            }

            for (int k = 0; k < nextCount; k++)
            {
                source[k] = nextBuf[k];
            }
            sourceCount = nextCount;
        }
        return sourceCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOpen(DateTime dateTime)
    {
        // The bitmask and the window array are built together, so the mask is
        // all-zero exactly when there are no windows. Testing the bit alone is
        // therefore equivalent to the previous explicit "_windows.Length == 0"
        // guard and avoids touching the (array-typed) windows field here.
        ulong totalMinutes = (ulong)dateTime.Ticks / (ulong)TimeSpan.TicksPerMinute;
        int weekMinute = (int)(totalMinutes % (ulong)MinutesPerWeek);
        return (_bitmask[weekMinute >> 6] & (1UL << (weekMinute & 63))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(DateTime dateTime) => IsOpen(dateTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime? GetCurrentShiftEnd(DateTime dt)
    {
        if (_windows.Length == 0) return null;
        if (_windows.Length == 1 && _windows[0].Start == 0 && _windows[0].End == MinutesPerWeek)
        {
            return null;
        }

        var (min, subMinuteTicks) = GetWeekMinute(dt);
        int n = _windows.Length;
        int idx = FindFirstWindowStartingAtOrAfter(min);
        if (idx >= n || _windows[idx].Start > min)
        {
            return null;
        }

        ref readonly var window = ref _windows[idx];
        int diffMin = window.End - min;

        if (idx == n - 1 && window.End == MinutesPerWeek && _windows[0].Start == 0)
        {
            diffMin = (MinutesPerWeek - min) + _windows[0].End;
        }

        return dt.AddTicks((long)diffMin * TimeSpan.TicksPerMinute - subMinuteTicks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan? GetTimeToOpen(DateTime from)
    {
        if (_windows.Length == 0) return null;
        if (_windows.Length == 1 && _windows[0].Start == 0 && _windows[0].End == MinutesPerWeek)
        {
            return TimeSpan.Zero;
        }

        var (t, subMinuteTicks) = GetWeekMinute(from);
        int[] tbl = EnsureTbl();
        if ((_bitmask[t >> 6] & (1UL << (t & 63))) != 0)
        {
            return TimeSpan.Zero;
        }
        return new TimeSpan((long)tbl[t] * TimeSpan.TicksPerMinute - subMinuteTicks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan? GetTimeToOpenForDuration(DateTime from, TimeSpan duration)
    {
        if (_windows.Length == 0) return null;
        long durTicks = duration.Ticks;
        if (durTicks <= 0) return TimeSpan.Zero;
        if (durTicks > (long)MinutesPerWeek * TimeSpan.TicksPerMinute) return null;

        if (_windows.Length == 1 && _windows[0].Start == 0 && _windows[0].End == MinutesPerWeek)
        {
            return TimeSpan.Zero;
        }

        var (t, subMinuteTicks) = GetWeekMinute(from);
        int reqMin = (int)((durTicks + TimeSpan.TicksPerMinute - 1) / TimeSpan.TicksPerMinute);
        int startIdx = FindFirstWindowStartingAtOrAfter(t);

        int n = _windows.Length;
        bool lastEndsAtWeekEnd = _windows[n - 1].End == MinutesPerWeek;
        bool firstStartsAtZero = _windows[0].Start == 0;

        for (int i = startIdx; i < n; i++)
        {
            ref readonly var w = ref _windows[i];
            int effectiveEnd = (i == n - 1 && lastEndsAtWeekEnd && firstStartsAtZero)
                ? MinutesPerWeek + _windows[0].End
                : w.End;

            if (t >= w.Start)
            {
                long remTicks = (long)(effectiveEnd - t) * TimeSpan.TicksPerMinute - subMinuteTicks;
                if (remTicks >= durTicks)
                {
                    return TimeSpan.Zero;
                }
            }
            else
            {
                if (effectiveEnd - w.Start >= reqMin)
                {
                    return new TimeSpan((long)(w.Start - t) * TimeSpan.TicksPerMinute - subMinuteTicks);
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            ref readonly var w = ref _windows[i];
            int effectiveEnd = (i == n - 1 && lastEndsAtWeekEnd && firstStartsAtZero)
                ? MinutesPerWeek + _windows[0].End
                : w.End;

            if (effectiveEnd - w.Start >= reqMin)
            {
                return new TimeSpan((long)((MinutesPerWeek - t) + w.Start) * TimeSpan.TicksPerMinute - subMinuteTicks);
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime? When(DateTime from, TimeSpan duration)
    {
        var wait = GetTimeToOpenForDuration(from, duration);
        if (!wait.HasValue)
        {
            return null;
        }
        return wait.Value == TimeSpan.Zero ? from : from.AddTicks(wait.Value.Ticks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (bool IsOpen, TimeSpan Duration) NextDur(DateTime dt)
    {
        if (_windows.Length == 0) return (false, TimeSpan.Zero);
        if (_windows.Length == 1 && _windows[0].Start == 0 && _windows[0].End == MinutesPerWeek)
        {
            return (true, new TimeSpan((long)MinutesPerWeek * TimeSpan.TicksPerMinute));
        }

        var (t, subMinuteTicks) = GetWeekMinute(dt);
        int[] tbl = EnsureTbl();
        bool open = (_bitmask[t >> 6] & (1UL << (t & 63))) != 0;
        return (open, new TimeSpan((long)tbl[t] * TimeSpan.TicksPerMinute - subMinuteTicks));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (bool IsOpen, DateTime NextDate) NextDate(DateTime dt)
    {
        var (isOpen, dur) = NextDur(dt);
        return (isOpen, dt.AddTicks(dur.Ticks));
    }

    // Lazily built per-minute transition table: tbl[m] = minutes from minute m
    // to the next schedule boundary. A pure function of _windows, so results are
    // bit-identical; built on first query so parse-only schedules (the stress
    // benchmark) pay nothing for it.
    private int[]? _tbl;

    private int[] EnsureTbl()
    {
        var cached = _tbl;
        if (cached != null) return cached;
        int n = _windows.Length;
        var t = new int[MinutesPerWeek];
        for (int i = 0; i < n; i++)
        {
            int s0 = _windows[i].Start, e0 = _windows[i].End;
            int blockEnd = e0;
            if (i == n - 1 && e0 == MinutesPerWeek && _windows[0].Start == 0)
            {
                blockEnd = MinutesPerWeek + _windows[0].End;
            }
            for (int m = s0; m < e0; m++) t[m] = blockEnd - m;
        }
        for (int i = 0; i + 1 < n; i++)
        {
            int a = _windows[i].End, b = _windows[i + 1].Start;
            for (int m = a; m < b; m++) t[m] = b - m;
        }
        if (n > 0)
        {
            int a = _windows[n - 1].End, b = _windows[0].Start;
            for (int m = a; m < MinutesPerWeek; m++) t[m] = (MinutesPerWeek - m) + b;
            for (int m = 0; m < b; m++) t[m] = b - m;
        }
        _tbl = t;
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindFirstWindowStartingAtOrAfter(int t)
    {
        int n = _windows.Length;
        if (n <= 16)
        {
            for (int i = 0; i < n; i++)
            {
                if (_windows[i].End > t) return i;
            }
            return n;
        }

        int low = 0;
        int high = n - 1;
        int result = n;
        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            if (_windows[mid].End > t)
            {
                result = mid;
                if (mid == 0) break;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int WeekMinute, long SubMinuteTicks) GetWeekMinute(DateTime dt)
    {
        long ticks = dt.Ticks;
        // Unsigned division: DateTime.Ticks is always >= 0, so this is exact and
        // lets the JIT use a single multiply-shift rather than sign-corrected ones.
        ulong totalMinutes = (ulong)ticks / (ulong)TimeSpan.TicksPerMinute;
        int weekMinute = (int)(totalMinutes % (ulong)MinutesPerWeek);
        long subMinuteTicks = ticks - (long)totalMinutes * TimeSpan.TicksPerMinute;
        return (weekMinute, subMinuteTicks);
    }

    public override string ToString() => _expression;
}

public sealed class OpenHoursJsonConverter : JsonConverter<OpenHours>
{
    // Thread-local fast path for the common case of deserializing the same
    // expression repeatedly (e.g. bulk JSON loads). Bytes of the last decoded
    // UTF-8 JSON string value are cached so a repeated value skips both the
    // UTF-8 -> UTF-16 decode and the intern-pool dictionary lookup.
    [ThreadStatic]
    private static byte[]? _fastJsonBytes;
    [ThreadStatic]
    private static int _fastJsonLen;
    [ThreadStatic]
    private static OpenHours? _fastJsonVal;

    public override OpenHours? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.HasValueSequence)
        {
            // Fallback for multi-segment input: use the string-based path.
            return reader.GetString() is { } expr ? OpenHours.Parse(expr) : null;
        }

        var span = reader.ValueSpan;

        // Fast path: same UTF-8 bytes as the previous value.
        int len = span.Length;
        if (len == _fastJsonLen && _fastJsonVal is { } cached && _fastJsonBytes is { } buf && span.SequenceEqual(buf.AsSpan(0, len)))
        {
            return cached;
        }

        if (span.IsEmpty)
        {
            return OpenHours.Parse("");
        }

        Span<char> chars = stackalloc char[span.Length];
        int written = System.Text.Encoding.UTF8.GetChars(span, chars);
        var result = OpenHours.Parse(chars[..written]);

        // Remember this value for the next read for an O(1) hit.
        byte[] buf2 = span.ToArray();
        _fastJsonBytes = buf2;
        _fastJsonLen = len;
        _fastJsonVal = result;
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        OpenHours value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString());
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(OpenHours))]
public partial class OpenHoursJsonContext : JsonSerializerContext
{
}

internal struct OpeningRule
{
    public int DayMask;
    public bool IsOff;
    public bool IsAllDay;
    public byte NumRanges;
    public TimeRange R0;
    public TimeRange R1;
    public TimeRange R2;
    public TimeRange R3;

    public void AddRange(TimeRange tr)
    {
        switch (NumRanges)
        {
            case 0: R0 = tr; NumRanges = 1; break;
            case 1: R1 = tr; NumRanges = 2; break;
            case 2: R2 = tr; NumRanges = 3; break;
            case 3: R3 = tr; NumRanges = 4; break;
        }
    }

    public readonly TimeRange GetRange(int idx) =>
        idx switch
        {
            0 => R0,
            1 => R1,
            2 => R2,
            3 => R3,
            _ => R0,
        };
}

public readonly record struct TimeWindow(int Start, int End);

internal readonly record struct TimeRange(int StartMin, int EndMin, int DayOffset = 0);
