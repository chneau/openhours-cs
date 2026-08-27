# Reference tests from opening_hours.js

`ReferenceTests.cs` ports the point-in-time open/closed assertions from the
original [opening_hours.js test suite](https://github.com/opening-hours/opening_hours.js/blob/main/test/test.js).

The reference suite is made up of `test.addTest(...)` (336), `addShouldFail`
(30), `addShouldWarn` (4), `addStructuredWarnings` (35), `addCompMatchingRule`
(5), `addPrettifyValue` (19), `addEqualTo` (6), and `addNextChangeTest` (3),
plus 14 `test/unit` cases. Under the "don't grow the public API" constraint,
only the interval-based `addTest` cases can be ported here, and only the ones
this parser already evaluates identically.

## What was ported

For every `test.addTest(...)` block in the reference suite whose expression
variants this parser accepts **and** evaluates to the **same open intervals**,
we assert `IsOpen` at: every expected-interval boundary (`start-1min`, `start`,
`start+1min`, midpoint, `end-1min`, `end`), the query-window start, and a coarse
daily grid inside the window.

That yields **48 portable reference cases** (this repo covers them all). They
exercise: simple + comma/`;`-separated time ranges, `off`/`closed` overrides,
always-closed schedules, overnight spans (e.g. `22:00-02:00`), day-selector
lists and wrap-around ranges, "no time means all day" selectors, 24h schedules
and `24/7`.

## What was NOT ported (outside this parser's grammar / the public API)

- open-end / variable end (`17:00+`, `sunset`, `dawn`, `14:00-sunset+`, …)
- am/pm, dot and unicode time-separator tolerance
- "additional"/"exception" rules that re-select the same weekday via `,`/`;`
- holidays (`PH`, `SH`) and region/*Nominatim* data
- variable days (month/week/day numbers, `Mo[1]`, `Jul-Aug`, `2025 Jul 27`)
- comments (`"..."`), `prettifyValue`, warnings, `isWeekStable`
- the `addShouldFail`/`addShouldWarn`/`addStructuredWarnings`/
  `addPrettifyValue`/`addEqualTo`/`addCompMatchingRule`/`addNextChangeTest`
  cases — they rely on APIs this library does not expose.

## Per-language differences

This C# port parses the full portable set (48 reference cases). The sibling
ports (`openhours`, `openhours-java`, `openhours-rs`) are independent parsers
and — where a language's parser does not implement the same sub-grammar, e.g.
mid-expression `24/7`/`open`, or no-space `We22:00-02:00` — exclude those
specific reference variants. See each repo's own reference test file header.
