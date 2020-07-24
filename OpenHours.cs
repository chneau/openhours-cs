using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace Chneau.Time
{
    public class OpenHours
    {
        internal List<UInt32> points = new List<UInt32>(); // 	0 to 4_294_967_295, which is good since > 60*60*24*7 = 604_800
        private static Dictionary<string, int> DAYS = new Dictionary<string, int> { { "su", 0 }, { "mo", 1 }, { "tu", 2 }, { "we", 3 }, { "th", 4 }, { "fr", 5 }, { "sa", 6 } };

        internal static String CleanInput(String input)
        {
            var result = input.ToLower();
            result = result.Trim();
            result = result.Replace(", ", ",");
            result = result.Replace(" ,", ",");
            result = Regex.Replace(result, @"\s+", " ");
            return result;
        }

        internal static IEnumerable<int> SimplifyDays(String input)
        {
            var days = new HashSet<int>();
            foreach (var comma in input.Split(","))
            {
                if (comma.Contains("-"))
                {
                    var split = comma.Split("-");
                    if (split.Length != 2) continue;
                    if (!DAYS.ContainsKey(split[0])) continue;
                    if (!DAYS.ContainsKey(split[1])) continue;
                    var from = DAYS.GetValueOrDefault(split[0]);
                    var to = DAYS.GetValueOrDefault(split[1]);
                    while (from != to)
                    {
                        days.Add(from);
                        from++;
                        if (from == 7)
                        {
                            from = 0;
                        }
                    };
                    days.Add(from);
                    continue;
                }
                if (DAYS.ContainsKey(comma))
                {
                    days.Add(DAYS.GetValueOrDefault(comma));
                }
            }
            var sorted = new List<int>(days);
            sorted.Sort();
            return sorted;
        }

        internal void BuildTimes(String input)
        {

        }

        public static OpenHours From(String input)
        {
            var oh = new OpenHours();
            oh.BuildTimes(input);
            return oh;
        }
    }
}
