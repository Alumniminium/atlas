using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using atlas.Servers;

namespace atlas
{
    public static class Statistics
    {
        public static Dictionary<DateOnly, Stats> StatsByDate { get; set; } = new();

        public static void Load()
        {
            if (!File.Exists("/etc/atlas/stats.json"))
                return;

            var json = File.ReadAllText("/etc/atlas/stats.json");
            StatsByDate = JsonSerializer.Deserialize<Dictionary<DateOnly, Stats>>(json);
        }

        public static void AddRequest(Context ctx)
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            if (!StatsByDate.ContainsKey(date))
                StatsByDate.Add(date, new Stats());

            if (!StatsByDate[date].GeminiFiles.ContainsKey(ctx.Request))
                StatsByDate[date].GeminiFiles.Add(ctx.Request, 0);

            StatsByDate[date].GeminiFiles[ctx.Request]++;
            StatsByDate[date].GeminiHits++;
            StatsByDate[date].GeminiBytesReceived += ctx.Request.Length;
        }
        public static void AddResponse(Response response)
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            if (!StatsByDate.ContainsKey(date))
                StatsByDate.Add(date, new Stats());

            StatsByDate[date].GeminiBytesSent += response.Data.Length;
        }

        public static Response Get()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Atlas Statistics");
            sb.AppendLine("## Hits");
            sb.AppendLine("```");
            sb.AppendLine(PlotHitsByMonth());
            sb.AppendLine("```");
            sb.AppendLine("## Requests");
            sb.AppendLine("```");
            sb.AppendLine(PlotPopularRequests());
            sb.AppendLine("```");
            sb.AppendLine("## Bandwidth (Month)");
            sb.AppendLine("```");
            sb.AppendLine(PlotTotalBandwidthByMonth());
            sb.AppendLine("```");
            sb.AppendLine("## Bandwidth (Day)");
            sb.AppendLine("```");
            sb.AppendLine(PlotTotalBandwidthByDay());
            sb.AppendLine("```");
            Save();
            return Response.Ok(Encoding.UTF8.GetBytes(sb.ToString()).AsMemory(), "text/gemini", false);
        }

        private static string PlotHitsByMonth()
        {
            var monthlyStats = StatsByDate.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                Hits = x.Sum(y => y.Value.TotalHits)
            }).OrderBy(x => x.Month).ToList();

            var dict = new Dictionary<string, long>();
            foreach (var stat in monthlyStats)
            {
                if (stat.Month == 1) dict.Add("Jan", stat.Hits);
                if (stat.Month == 2) dict.Add("Feb", stat.Hits);
                if (stat.Month == 3) dict.Add("Mar", stat.Hits);
                if (stat.Month == 4) dict.Add("Apr", stat.Hits);
                if (stat.Month == 5) dict.Add("May", stat.Hits);
                if (stat.Month == 6) dict.Add("Jun", stat.Hits);
                if (stat.Month == 7) dict.Add("Jul", stat.Hits);
                if (stat.Month == 8) dict.Add("Aug", stat.Hits);
                if (stat.Month == 9) dict.Add("Sep", stat.Hits);
                if (stat.Month == 10) dict.Add("Oct", stat.Hits);
                if (stat.Month == 11) dict.Add("Nov", stat.Hits);
                if (stat.Month == 12) dict.Add("Dec", stat.Hits);
            }

            var graph = new AsciiBarChart(dict);
            var lines = graph.DrawVertical(20);
            return string.Join(Environment.NewLine, lines);
        }

        private static string PlotTotalBandwidthByMonth()
        {
            var monthlyStats = StatsByDate.GroupBy(x => x.Key.Month).Select(x => new
            {
                Month = x.Key,
                Bandwidth = x.Sum(y => y.Value.TotalBytesSent) + x.Sum(y => y.Value.TotalBytesReceived)
            }).OrderBy(x => x.Month).ToList();

            var dict = new Dictionary<string, long>();
            foreach (var stat in monthlyStats)
            {
                if (stat.Month == 1) dict.Add("Jan", stat.Bandwidth);
                if (stat.Month == 2) dict.Add("Feb", stat.Bandwidth);
                if (stat.Month == 3) dict.Add("Mar", stat.Bandwidth);
                if (stat.Month == 4) dict.Add("Apr", stat.Bandwidth);
                if (stat.Month == 5) dict.Add("May", stat.Bandwidth);
                if (stat.Month == 6) dict.Add("Jun", stat.Bandwidth);
                if (stat.Month == 7) dict.Add("Jul", stat.Bandwidth);
                if (stat.Month == 8) dict.Add("Aug", stat.Bandwidth);
                if (stat.Month == 9) dict.Add("Sep", stat.Bandwidth);
                if (stat.Month == 10) dict.Add("Oct", stat.Bandwidth);
                if (stat.Month == 11) dict.Add("Nov", stat.Bandwidth);
                if (stat.Month == 12) dict.Add("Dec", stat.Bandwidth);
            }

            var graph = new AsciiBarChart(dict);
            var lines = graph.DrawVertical(20);
            return string.Join(Environment.NewLine, lines);
        }

        private static string PlotTotalBandwidthByDay()
        {
            var monthlyStats = StatsByDate.Where(x => x.Key >= DateOnly.FromDateTime(DateTime.Today.AddDays(-6))).Select(x => new
            {
                Day = x.Key.DayOfWeek,
                Bandwidth = x.Value.TotalBytesReceived + x.Value.TotalBytesSent
            }).OrderBy(x => x.Day).ToDictionary(x => x.Day.ToString()[..3], x => x.Bandwidth);

            var graph = new AsciiBarChart(monthlyStats);
            var lines = graph.DrawHorizontal(60);
            return string.Join(Environment.NewLine, lines);
        }

        private static string PlotPopularRequests()
        {
            var sb = new StringBuilder();
            var requests = StatsByDate.SelectMany(x => x.Value.GeminiFiles).GroupBy(x => x.Key.Split(':')[1][2..]).Select(x => new
            {
                Request = x.Key,
                Hits = x.Sum(y => y.Value)
            }).OrderByDescending(x => x.Hits).Take(20).Distinct().ToDictionary(x => x.Request, x => x.Hits);

            var graph = new AsciiBarChart(requests);
            var lines = graph.DrawHorizontal(120, false);
            return string.Join(Environment.NewLine, lines);
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(StatsByDate);
                File.WriteAllText("/etc/atlas/stats.json", json);
            }
            catch
            {
                Console.WriteLine("Failed to save stats. Do I have write permissions to /etc/atlas/stats.json?");
            }
        }
    }
}