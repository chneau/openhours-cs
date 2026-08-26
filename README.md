# openhours-cs

A high-performance, zero-allocation C# (.NET 10) parser and interval-math evaluator for OpenStreetMap [`opening_hours`](https://wiki.openstreetmap.org/wiki/Key:opening_hours) specifications.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## ⚡ Features & Performance

- **$O(1)$ Hardware-Accelerated Bitmask Table**: Evaluates `IsOpen` in **~2.0 nanoseconds** (>500 Million ops/sec) via embedded `[InlineArray(158)] Bitmask158` scalar bit tests (`BTQ`).
- **Zero-Allocation Interval Math**: Zero heap allocations on query paths (`IsOpen`, `GetTimeToOpen`, `GetTimeToOpenForDuration`, `When`, `NextDur`, `NextDate`).
- **Span-Based Parsing**: Zero string allocations when parsing UTF-8 bytes or raw `ReadOnlySpan<char>`.
- **Lock-Free Interning**: Automatic lock-free deduplication and caching of parsed expressions.
- **Overnight Shifts**: Full support for shifts spanning midnight (e.g. `Mo 22:00-04:00`, `Su 22:00-04:00`).
- **Overrides & Exclusions**: Handles `off` / `closed` rules overriding previous rules (e.g. `Mo-Su 00:00-24:00; Tu 12:00-13:00 off`).
- **Duration Availability**: Find wait times for contiguous tasks of duration $D$ (`GetTimeToOpenForDuration` / `When`).
- **Source-Generated JSON**: Native `System.Text.Json` converter and `OpenHoursJsonContext` deserializing in **~220 nanoseconds**.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package Chneau.Time
```

### Usage Example

```csharp
using Chneau.Time;

// 1. Parse an OSM opening_hours string
var oh = OpenHours.Parse("Mo-Fr 08:00-12:00, 13:00-17:00; Sa 08:00-12:00");

var monday10am = new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);

// 2. Fast point-in-time check (2.0 ns/op)
bool isOpen = oh.IsOpen(monday10am); // true

// 3. Current shift end
DateTime? shiftEnd = oh.GetCurrentShiftEnd(monday10am); // 2026-05-18 12:00:00 UTC

// 4. Time to next open
var tuesdayLunch = new DateTime(2026, 5, 19, 12, 30, 0, DateTimeKind.Utc);
TimeSpan? timeToOpen = oh.GetTimeToOpen(tuesdayLunch); // 00:30:00 (opens at 13:00)

// 5. Find when a 3-hour job can be serviced
TimeSpan? waitFor3h = oh.GetTimeToOpenForDuration(tuesdayLunch, TimeSpan.FromHours(3));
DateTime? whenCanStart = oh.When(tuesdayLunch, TimeSpan.FromHours(3)); // 2026-05-19 13:00:00 UTC

// 6. Next state transitions
var (isOpenNow, durationRemaining) = oh.NextDur(monday10am);
var (_, nextTransitionDate) = oh.NextDate(monday10am); // 2026-05-18 12:00:00 UTC
```

---

## 📊 Benchmark Suite (.NET 10 on AMD Ryzen 9)

| # | Workload | Calls | Latency / Op | Throughput |
| :--- | :--- | :--- | :--- | :--- |
| **1** | **`IsOpen` (Rolling timeline)** | 100,000 | **2.0 ns** | 500,000,000 ops/sec |
| **2** | **`IsOpen` (Pure call)** | 1,000,000 | **2.0 ns** | 500,000,000 ops/sec |
| **3** | **`GetTimeToOpen`** | 10,000 | **8.0 ns** | 125,000,000 ops/sec |
| **4** | **`GetTimeToOpenForDuration`** | 10,000 | **9.0 ns** | 111,000,000 ops/sec |
| **5** | **`When`** | 10,000 | **12.0 ns** | 83,000,000 ops/sec |
| **6** | **`NextDur`** | 10,000 | **67.0 ns** | 15,000,000 ops/sec |
| **7** | **`NextDate`** | 10,000 | **100.0 ns** | 10,000,000 ops/sec |
| **8** | **`Parse` (Cached)** | 1,000 | **26.0 ns** | 38,000,000 ops/sec |
| **9** | **`JSON Deserialize`** | 1,000 | **220 ns** | 4,500,000 ops/sec |
| **10** | **Stress Test (5,000 unique objects)** | 5,000 | **0.50 µs/obj** | 2,000,000 objs/sec |

---

## 🛠️ Development & Quality Commands

```bash
# Run all unit tests
dotnet test

# Run tests in continuous watch mode during development
dotnet watch test

# Run full release build
dotnet build -c Release
```

---

## 📄 License

MIT License. Copyright (c) 2026 chneau.
