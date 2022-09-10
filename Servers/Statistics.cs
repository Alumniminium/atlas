using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using atlas.Servers;

namespace atlas
{
    public class Stats
    {
        public int TotalHits => GeminiHits + SpartanHits;
        public int TotalBytesSent => GeminiBytesSent + SpartanBytesSent;
        public int TotalBytesReceived => GeminiBytesReceived + SpartanBytesReceived;

        public int GeminiHits { get; set; } = new();
        public int GeminiBytesSent { get; set; } = new();
        public int GeminiBytesReceived { get; set; } = new();

        public int SpartanHits { get; set; } = new();
        public int SpartanBytesSent { get; set; } = new();
        public int SpartanBytesReceived { get; set; } = new();

        public Dictionary<string, int> SpartanFiles { get; set; } = new();
        public Dictionary<string, int> GeminiFiles { get; set; } = new();
    }

    public static class Statistics
    {
        public static Dictionary<DateOnly, Stats> DailyStats { get; set; } = new();

        // pupulate dailystats with random demo data
        public static void Populate()
        {
            // load stats.json if exists
            if (File.Exists("stats.json"))
            {
                var json = File.ReadAllText("stats.json");
                DailyStats = JsonSerializer.Deserialize<Dictionary<DateOnly, Stats>>(json);
            }
            else
            {
                // initialize dailystats with blank data
                for (var i = 0; i < 365; i++)
                {
                    var date = DateOnly.FromDateTime(DateTime.Now.AddDays(-i));
                    DailyStats.Add(date, new Stats());
                }
            }
        }

        public static void AddRequest(Context ctx)
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            if (!DailyStats.ContainsKey(date))
                DailyStats.Add(date, new Stats());

            if (ctx.IsGemini)
            {
                if (!DailyStats[date].GeminiFiles.ContainsKey(ctx.Request))
                    DailyStats[date].GeminiFiles.Add(ctx.Request, 0);

                DailyStats[date].GeminiFiles[ctx.Request]++;
                DailyStats[date].GeminiHits++;
                DailyStats[date].GeminiBytesReceived += ctx.Request.Length;
            }
            else
            {
                if (!DailyStats[date].SpartanFiles.ContainsKey(ctx.Request))
                    DailyStats[date].SpartanFiles.Add(ctx.Request, 0);

                DailyStats[date].SpartanFiles[ctx.Request]++;
                DailyStats[date].SpartanHits++;
                DailyStats[date].SpartanBytesReceived += ctx.Request.Length;
            }
        }
        public static void AddResponse(Response response)
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            if (!DailyStats.ContainsKey(date))
                DailyStats.Add(date, new Stats());

            if (response.IsGemini)
                DailyStats[date].GeminiBytesSent += response.Data.Length;
            else
                DailyStats[date].SpartanBytesSent += response.Data.Length;
        }

        public static Response GetStatistics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Atlas Statistics");
            sb.AppendLine("## Hits");
            sb.AppendLine(PlotHitsByMonth());
            sb.AppendLine("## Requests");
            sb.AppendLine(PlotPopularRequests());
            sb.AppendLine("## Bandwidth (Day)");
            sb.AppendLine(PlotTotalBandwidthByDay());
            sb.AppendLine("## Bandwidth (Month)");
            sb.AppendLine(PlotTotalBandwidthByMonth());

            sb.AppendLine("# Gemini");
            sb.AppendLine("## Hits");
            sb.AppendLine(PlotGeminiHitsByMonth());
            sb.AppendLine("## Bandwidth (Month)");
            sb.AppendLine(PlotGeminiTotalBandwidthByMonth());
            sb.AppendLine("## Bandwidth (Day)");
            sb.AppendLine(PlotGeminiTotalBandwidthByDay());
            sb.AppendLine("## Requests");
            sb.AppendLine(PlotGeminiPopularRequests());

            sb.AppendLine("# Spartan");
            sb.AppendLine("## Hits");
            sb.AppendLine(PlotSpartanHitsByMonth());
            sb.AppendLine("## Bandwidth (Month)");
            sb.AppendLine(PlotSpartanTotalBandwidthByMonth());
            sb.AppendLine("## Bandwidth (Day)");
            sb.AppendLine(PlotSpartanTotalBandwidthByDay());
            sb.AppendLine("## Requests");
            sb.AppendLine(PlotSpartanPopularRequests());

            return Response.Ok(Encoding.UTF8.GetBytes(sb.ToString()).AsMemory(), "text/gemini", false);
        }

        // graph dailystats in ascii horizontal bar chart by month
        public static string PlotHitsByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                Hits = x.Sum(y => y.Value.TotalHits)
            }).OrderBy(x => x.Month).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.Hits);

            if (max == 0)
                return "No data to plot";

            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;

            for (int i = 0; i < height; i++)
            {
                var y = (int)Math.Round((double)max / height * (height - i));
                _ = sb.Append($"{y}\t");
                foreach (var stat in monthlyStats)
                {
                    var x = (int)Math.Round((double)stat.Hits / max * height);
                    if (x <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotTotalBandwidthByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                BytesSent = x.Sum(y => y.Value.TotalBytesSent),
                BytesReceived = x.Sum(y => y.Value.TotalBytesReceived)
            }).OrderBy(x => x.Month).ToList();

            if (monthlyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.BytesSent + x.BytesReceived);
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in monthlyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotTotalBandwidthByDay()
        {
            // group hits by month
            var weeklyStats = DailyStats.Where(x => x.Key >= DateOnly.FromDateTime(DateTime.Today.AddDays(-7))).Select(x => new
            {
                Day = x.Key.DayOfWeek,
                BytesSent = x.Value.TotalBytesSent,
                BytesReceived = x.Value.TotalBytesReceived
            }).OrderBy(x => x.Day).ToList();

            if (weeklyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = weeklyStats.Max(x => x.BytesSent + x.BytesReceived);
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in weeklyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("    ");
                    else
                        sb.Append("████");
                    sb.Append("    ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in weeklyStats)
                sb.Append($"{dayNames[(int)stat.Day]}     ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        // graph most popular request names in ascii pie chart
        public static string PlotPopularRequests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("```");
            // sum up all requests and sort by hits
            var requests = DailyStats.SelectMany(x => x.Value.GeminiFiles).GroupBy(x => x.Key).Select(x => new
            {
                Request = x.Key,
                Hits = x.Sum(y => y.Value)
            }).OrderByDescending(x => x.Hits).Take(20).ToList();

            if (requests.Count == 0)
                return "No data to plot";

            var total = requests.Sum(x => x.Hits);
            var max = requests.Max(x => x.Hits);
            var counter = 1;
            foreach (var request in requests)
            {
                _ = sb.Append($"[{counter}]\t");

                for (int i = 0; i < 65; i++)
                {
                    if (request.Hits / (max / 65f) > i)
                        sb.Append("█");
                    else
                        sb.Append(" ");
                }
                sb.Append($"\t{request.Hits * 100 / total}%");
                counter++;
                sb.AppendLine();
            }
            sb.AppendLine();
            counter = 1;
            foreach (var req in requests)
            {
                var name = string.Join('/', req.Request.Split('/')[3..]);
                name = name == "" ? "index.gmi" : name;
                _ = sb.AppendLine($"[{counter}]\t{name}");
                counter++;
            }
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotGeminiHitsByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                Hits = x.Sum(y => y.Value.GeminiHits)
            }).OrderBy(x => x.Month).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.Hits);

            if (max == 0)
                return "No data to plot";

            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;

            for (int i = 0; i < height; i++)
            {
                var y = (int)Math.Round((double)max / height * (height - i));
                _ = sb.Append($"{y}\t");
                foreach (var stat in monthlyStats)
                {
                    var x = (int)Math.Round((double)stat.Hits / max * height);
                    if (x <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotGeminiTotalBandwidthByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                BytesSent = x.Sum(y => y.Value.GeminiBytesSent),
                BytesReceived = x.Sum(y => y.Value.GeminiBytesReceived)
            }).OrderBy(x => x.Month).ToList();

            if (monthlyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.BytesSent + x.BytesReceived);
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in monthlyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotGeminiTotalBandwidthByDay()
        {
            // group hits by month
            var weeklyStats = DailyStats.Where(x => x.Key >= DateOnly.FromDateTime(DateTime.Today.AddDays(-7))).Select(x => new
            {
                Day = x.Key.DayOfWeek,
                BytesSent = x.Value.GeminiBytesSent,
                BytesReceived = x.Value.GeminiBytesReceived
            }).OrderBy(x => x.Day).ToList();

            if (weeklyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = weeklyStats.Max(x => x.BytesSent + x.BytesReceived);
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in weeklyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("    ");
                    else
                        sb.Append("████");
                    sb.Append("    ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in weeklyStats)
                sb.Append($"{dayNames[(int)stat.Day]}     ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }
        public static string PlotSpartanHitsByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                Hits = x.Sum(y => y.Value.SpartanHits)
            }).OrderBy(x => x.Month).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.Hits);

            if (max == 0)
                return "No data to plot";

            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;

            for (int i = 0; i < height; i++)
            {
                var y = (int)Math.Round((double)max / height * (height - i));
                _ = sb.Append($"{y}\t");
                foreach (var stat in monthlyStats)
                {
                    var x = (int)Math.Round((double)stat.Hits / max * height);
                    if (x <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotSpartanTotalBandwidthByMonth()
        {
            // group hits by month
            var monthlyStats = DailyStats.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                BytesSent = x.Sum(y => y.Value.SpartanBytesSent),
                BytesReceived = x.Sum(y => y.Value.SpartanBytesReceived)
            }).OrderBy(x => x.Month).ToList();

            if (monthlyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = monthlyStats.Max(x => x.BytesSent + x.BytesReceived);
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in monthlyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("   ");
                    else
                        sb.Append("███");
                    sb.Append("   ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in monthlyStats)
                sb.Append($"{monthNames[stat.Month - 1]}   ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotSpartanTotalBandwidthByDay()
        {
            // group hits by month
            var weeklyStats = DailyStats.Where(x => x.Key >= DateOnly.FromDateTime(DateTime.Today.AddDays(-7))).Select(x => new
            {
                Day = x.Key.DayOfWeek,
                BytesSent = x.Value.SpartanBytesSent,
                BytesReceived = x.Value.SpartanBytesReceived
            }).OrderBy(x => x.Day).ToList();

            if (weeklyStats.Count == 0)
                return "No data to plot";

            // plot hits by month as vertical bar chart
            var sb = new StringBuilder();
            sb.AppendLine("```");
            var max = weeklyStats.Max(x => x.BytesSent + x.BytesReceived);
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            const int height = 15;
            for (int i = 0; i < height; i++)
            {
                // append kilobytes or megabytes on y axis
                if (max / 1000 > 1000)
                    _ = sb.Append($"{(max - (max / height * i)) / 1000000}M\t");
                else
                    _ = sb.Append($"{(max - (max / height * i)) / 1000}K\t");

                foreach (var stat in weeklyStats)
                {
                    var val = (int)Math.Round((double)(stat.BytesSent + stat.BytesReceived) / max * height);
                    if (val <= i)
                        sb.Append("    ");
                    else
                        sb.Append("████");
                    sb.Append("    ");
                }
                sb.AppendLine();
            }
            sb.Append(" \t");
            foreach (var stat in weeklyStats)
                sb.Append($"{dayNames[(int)stat.Day]}     ");
            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string PlotGeminiPopularRequests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("```");
            // sum up all requests and sort by hits
            var requests = DailyStats.SelectMany(x => x.Value.GeminiFiles).GroupBy(x => x.Key).Select(x => new
            {
                Request = x.Key,
                Hits = x.Sum(y => y.Value)
            }).OrderByDescending(x => x.Hits).Take(20).ToList();

            if (requests.Count == 0)
                return "No data to plot";

            var total = requests.Sum(x => x.Hits);
            var max = requests.Max(x => x.Hits);
            var counter = 1;
            foreach (var request in requests)
            {
                _ = sb.Append($"[{counter}]\t");

                for (int i = 0; i < 65; i++)
                {
                    if (request.Hits / (max / 65f) > i)
                        sb.Append("█");
                    else
                        sb.Append(" ");
                }
                sb.Append($"\t{request.Hits * 100 / total}%");
                counter++;
                sb.AppendLine();
            }
            sb.AppendLine();
            counter = 1;
            foreach (var req in requests)
            {
                var name = string.Join('/', req.Request.Split('/')[3..]);
                name = name == "" ? "index.gmi" : name;
                _ = sb.AppendLine($"[{counter}]\t{name}");
                counter++;
            }
            sb.AppendLine("```");
            return sb.ToString();
        }
        public static string PlotSpartanPopularRequests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("```");
            // sum up all requests and sort by hits
            var requests = DailyStats.SelectMany(x => x.Value.SpartanFiles).GroupBy(x => x.Key).Select(x => new
            {
                Request = x.Key,
                Hits = x.Sum(y => y.Value)
            }).OrderByDescending(x => x.Hits).Take(20).ToList();

            if (requests.Count == 0)
                return "No data to plot";

            var total = requests.Sum(x => x.Hits);
            var max = requests.Max(x => x.Hits);
            var counter = 1;
            foreach (var request in requests)
            {
                _ = sb.Append($"[{counter}]\t");

                for (int i = 0; i < 65; i++)
                {
                    if (request.Hits / (max / 65f) > i)
                        sb.Append("█");
                    else
                        sb.Append(" ");
                }
                sb.Append($"\t{request.Hits * 100 / total}%");
                counter++;
                sb.AppendLine();
            }
            sb.AppendLine();
            counter = 1;
            foreach (var req in requests)
            {
                var name = string.Join('/', req.Request.Split('/')[3..]);
                name = name == "" ? "index.gmi" : name;
                _ = sb.AppendLine($"[{counter}]\t{name}");
                counter++;
            }
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static void Save()
        {
            var json = JsonSerializer.Serialize(DailyStats);
            File.WriteAllText("stats.json", json);
        }
    }
}