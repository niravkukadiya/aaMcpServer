// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
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

        public QueryHistoryTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_query_history";

        public string Description =>
            "Query historical time-series values for one or more tags from the AVEVA Historian. " +
            "Supports retrieval modes (Cyclic, Delta, Full, Interpolated, BestFit, Average, Min, " +
            "Max, Integral, Slope, Counter, ValueState, RoundTrip) and the wwCycleCount / " +
            "wwResolution time-domain extensions. Output defaults to compact format: epoch_ms " +
            "timestamps, columnar header, and for uniform-interval modes the per-row time is " +
            "dropped (header has t0+dt; row i = t0 + i*dt). Pass format='table' for the " +
            "verbose pipe-table format.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "One or more exact tag names.",
                },
                ["startTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Start, e.g. '2026-05-25 00:00:00' or ISO-8601.",
                },
                ["endTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "End, e.g. '2026-05-25 06:00:00' or ISO-8601.",
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
                    ["description"] = "Cyclic / aggregate-mode: evenly spaced cycles over the range.",
                },
                ["resolution"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Spacing between returned values in ms (alternative to cycleCount).",
                },
                ["maxRows"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Cap on rows returned (bounded by the server limit).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format ('compact').",
                },
            },
            ["required"] = new JArray { "tagNames", "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);
            var start = SqlSanitize.QuoteDateTime(Args.GetRequiredString(args, "startTime"), "startTime");
            var end = SqlSanitize.QuoteDateTime(Args.GetRequiredString(args, "endTime"), "endTime");
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
            {
                sb.Append(" AND wwCycleCount = 100");
            }
            sb.Append(" ORDER BY TagName, DateTime");

            var result = _client.Run(sb.ToString(), maxRows);

            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatHistory(result, mode, _cfg.HistorianTimesAreUtc,
                    "No historical data found for the requested tag(s) and range.")
                : result.ToText("No historical data found for the requested tag(s) and range.");
        }
    }
}
