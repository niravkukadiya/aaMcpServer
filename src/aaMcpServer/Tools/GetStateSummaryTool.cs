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
    /// <summary>Time-in-state statistics from StateSummaryHistory (for discrete tags).</summary>
    public sealed class GetStateSummaryTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetStateSummaryTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_state_summary";

        public string Description =>
            "For DISCRETE tags: time spent in each value (state) over a time range. Returns " +
            "one row per (tag, state) with StateCount, StateTimeTotal (ms), StateTimePercent. " +
            "Use to answer 'how long was the pump running today?' or 'what percentage of " +
            "yesterday was machine in alarm state?'.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Exact discrete tagnames.",
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
                "SELECT TagName, StartDateTime, EndDateTime, Value, " +
                "  StateCount, StateTimeTotal, StateTimePercent, OPCQuality " +
                "FROM StateSummaryHistory WHERE TagName IN (" + inList + ") " +
                "AND StartDateTime >= " + start + " AND EndDateTime <= " + end + " " +
                "AND wwCycleCount = " + cc + " ORDER BY TagName, StartDateTime, Value";
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No state-summary data for the requested tag(s) and range.")
                : r.ToText("No state-summary data.");
        }
    }
}
