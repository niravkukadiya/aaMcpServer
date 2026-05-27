// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Text;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    public sealed class QueryHistoryTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public QueryHistoryTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_query_history";

        public string Description =>
            "Query historical time-series values for one or more tags over a time range. " +
            "Supports retrieval modes (Cyclic, Delta, Full, Interpolated, BestFit, Average, " +
            "Min, Max, Integral, Slope, Counter, ValueState, RoundTrip) and the wwCycleCount " +
            "/ wwResolution time-domain extensions. Times accept ISO/SQL or shorthand " +
            "('now', 'today', 'last 1h'). Output defaults to a compact format: epoch_ms " +
            "timestamps; for uniform-interval modes the per-row time is dropped (header " +
            "has t0+dt; row i = t0 + i*dt). ~70% fewer tokens than the verbose table format.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "One or more exact tagnames.",
                },
                ["startTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Range start: ISO/SQL datetime or shorthand.",
                },
                ["endTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Range end: ISO/SQL datetime or shorthand.",
                },
                ["retrievalMode"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray
                    {
                        "Cyclic", "Delta", "Full", "Interpolated", "BestFit", "Average",
                        "Min", "Max", "Integral", "Slope", "Counter", "ValueState", "RoundTrip",
                    },
                    ["description"] = "Retrieval mode. Default 'Cyclic'.",
                },
                ["cycleCount"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of evenly spaced cycles. Default 100 for cycle-based modes.",
                },
                ["resolution"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Spacing in ms (alternative to cycleCount).",
                },
                ["maxRows"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Hard cap on returned rows.",
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
            var mode = SqlSanitize.RetrievalMode(Args.GetString(args, "retrievalMode", "Cyclic"));

            var maxRows = _cfg.MaxRows;
            if (Args.HasValue(args, "maxRows"))
                maxRows = SqlSanitize.ClampInt(Args.GetInt(args, "maxRows", _cfg.MaxRows), 1, _cfg.MaxRows);

            var sb = new StringBuilder();
            sb.Append("SELECT DateTime, TagName, vValue AS Value, Quality, QualityDetail ");
            sb.Append("FROM History WHERE TagName IN (").Append(inList).Append(") ");
            sb.Append("AND DateTime >= ").Append(start).Append(' ');
            sb.Append("AND DateTime <= ").Append(end).Append(' ');
            sb.Append("AND wwRetrievalMode = '").Append(mode).Append('\'');

            var hasCycle = Args.HasValue(args, "cycleCount");
            var hasRes = Args.HasValue(args, "resolution");
            if (hasCycle)
            {
                var cc = SqlSanitize.ClampInt(Args.GetInt(args, "cycleCount", 100), 1, 1000000);
                sb.Append(" AND wwCycleCount = ").Append(cc);
            }
            if (hasRes)
            {
                var res = SqlSanitize.ClampInt(Args.GetInt(args, "resolution", 1000), 1, int.MaxValue);
                sb.Append(" AND wwResolution = ").Append(res);
            }
            if (!hasCycle && !hasRes && mode != "Delta" && mode != "Full")
                sb.Append(" AND wwCycleCount = 100");
            sb.Append(" ORDER BY TagName, DateTime");

            var r = _client.Run(sb.ToString(), maxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatHistory(r, mode, _cfg.HistorianTimesAreUtc,
                    "No historical data found for the requested tag(s) and range.")
                : r.ToText("No historical data found.");
        }
    }
}
