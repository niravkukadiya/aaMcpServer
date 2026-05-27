// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>One-row summary (Min/Max/Avg/Integral/StdDev) per cycle from AnalogSummaryHistory.</summary>
    public sealed class GetSummaryTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetSummaryTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_summary";

        public string Description =>
            "Get statistical summary (Minimum, Maximum, Average, Integral, StdDev, PercentGood) " +
            "for one or more analog tags over a time range. Uses the AnalogSummaryHistory " +
            "view — much cheaper than running separate Min/Max/Avg queries. By default " +
            "returns ONE summary row per tag (cycleCount=1); pass cycleCount=N to split the " +
            "range into N intervals. Times accept ISO/SQL or shorthand ('today', 'last 24h').";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Exact analog tagnames.",
                },
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["cycleCount"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of intervals (default 1 = single summary per tag).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "tagNames", "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);
            var start = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "startTime"), "startTime"));
            var end = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "endTime"), "endTime"));
            var cc = SqlSanitize.ClampInt(Args.GetInt(args, "cycleCount", 1), 1, 1000);

            var sql =
                "SELECT TagName, StartDateTime, EndDateTime, Minimum, Maximum, Average, " +
                "  Integral, StdDev, PercentGood, OPCQuality " +
                "FROM AnalogSummaryHistory WHERE TagName IN (" + inList + ") " +
                "AND StartDateTime >= " + start + " AND EndDateTime <= " + end + " " +
                "AND wwCycleCount = " + cc + " ORDER BY TagName, StartDateTime";
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No summary data for the requested tag(s) and range.")
                : r.ToText("No summary data.");
        }
    }
}
