// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Text;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    public sealed class QueryAlarmsEventsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;

        public QueryAlarmsEventsTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_query_alarms_events";

        public string Description =>
            "Query alarm and event history over a time range from the AVEVA Historian alarm " +
            "store. Returns event records (timestamp, tag, severity/priority, condition, " +
            "operator, etc.). Optionally filter by a tag-name pattern. Default output is compact " +
            "(CSV with epoch_ms times); pass format='table' for the verbose pipe-table.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["startTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Start of the range, e.g. '2026-05-25 00:00:00' or ISO-8601.",
                },
                ["endTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "End of the range, e.g. '2026-05-25 23:59:59' or ISO-8601.",
                },
                ["tagName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional tag-name substring filter (requires a TagName column).",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Maximum records to return (1-5000, default 200).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format ('compact').",
                },
            },
            ["required"] = new JArray { "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            var source = SqlSanitize.Identifier(_cfg.AlarmsSource, "Alarms.Source");
            var timeCol = SqlSanitize.Identifier(_cfg.AlarmsTimeColumn, "Alarms.TimeColumn");

            var start = SqlSanitize.QuoteDateTime(Args.GetRequiredString(args, "startTime"), "startTime");
            var end = SqlSanitize.QuoteDateTime(Args.GetRequiredString(args, "endTime"), "endTime");
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 200), 1, _cfg.MaxRows);

            var sb = new StringBuilder();
            sb.Append("SELECT TOP (").Append(limit).Append(") * FROM ").Append(source);
            sb.Append(" WHERE ").Append(timeCol).Append(" >= ").Append(start);
            sb.Append(" AND ").Append(timeCol).Append(" <= ").Append(end);

            var tagName = Args.GetString(args, "tagName");
            if (!string.IsNullOrWhiteSpace(tagName))
                sb.Append(" AND TagName LIKE ").Append(SqlSanitize.QuoteLiteral("%" + tagName + "%"));
            sb.Append(" ORDER BY ").Append(timeCol);

            var result = _client.Run(sb.ToString(), limit);

            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(result, "his_query_alarms_events",
                    _cfg.HistorianTimesAreUtc, "No alarm/event records found for the range.")
                : result.ToText("No alarm/event records found for the range.");
        }
    }
}
