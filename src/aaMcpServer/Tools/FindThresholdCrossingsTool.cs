// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>Finds the moments a value crosses an absolute threshold using wwEdgeDetection.</summary>
    public sealed class FindThresholdCrossingsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public FindThresholdCrossingsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_find_threshold_crossings";

        public string Description =>
            "Find the moments an analog tag's value crossed above or below a threshold " +
            "(via wwEdgeDetection). Returns only the transition points, not the bulk " +
            "data — very token-efficient. Use to answer 'when did Temperature first " +
            "exceed 90 today?' or 'when did Pressure drop below 2.0 bar last hour?'. " +
            "Operators: '>', '<', '>=', '<=', '='. " +
            "Edge: 'leading' = first row to satisfy (default), 'trailing' = first row " +
            "to stop satisfying, 'both' = all transitions.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagName"] = new JObject { ["type"] = "string", ["description"] = "Exact tagname." },
                ["operator"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { ">", "<", ">=", "<=", "=" },
                    ["description"] = "Comparison operator.",
                },
                ["threshold"] = new JObject { ["type"] = "number", ["description"] = "Threshold value." },
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["edge"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "leading", "trailing", "both", "none" },
                    ["description"] = "Edge detection mode. Default 'leading'.",
                },
                ["resolution"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Sampling resolution in ms (default 1000).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "tagName", "operator", "threshold", "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            var tag = SqlSanitize.QuoteLiteral(Args.GetRequiredString(args, "tagName"));
            var op = Args.GetRequiredString(args, "operator").Trim();
            if (op != ">" && op != "<" && op != ">=" && op != "<=" && op != "=")
                throw new ToolException("Invalid operator '" + op + "'.");
            var th = (double)args["threshold"];
            var start = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "startTime"), "startTime"));
            var end = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "endTime"), "endTime"));
            var edge = (Args.GetString(args, "edge", "leading") ?? "leading").ToLowerInvariant();
            if (edge != "leading" && edge != "trailing" && edge != "both" && edge != "none")
                throw new ToolException("Invalid edge '" + edge + "'.");
            var edgeCanonical = char.ToUpperInvariant(edge[0]) + edge.Substring(1);
            var res = SqlSanitize.ClampInt(Args.GetInt(args, "resolution", 1000), 1, int.MaxValue);

            var sql =
                "SELECT DateTime, TagName, Value, Quality FROM History " +
                "WHERE TagName = " + tag + " " +
                "AND DateTime >= " + start + " AND DateTime <= " + end + " " +
                "AND wwRetrievalMode = 'Cyclic' AND wwResolution = " + res + " " +
                "AND Value " + op + " " + th.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " +
                "AND wwEdgeDetection = '" + edgeCanonical + "' " +
                "ORDER BY DateTime";
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc, "No crossings found.")
                : r.ToText("No crossings found.");
        }
    }
}
