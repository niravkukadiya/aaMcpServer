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
    /// <summary>Average alarm acknowledgement time by severity (per hour).</summary>
    public sealed class ResponseMetricsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public ResponseMetricsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_response_metrics";

        public string Description =>
            "Operator-response metrics: average acknowledge time (ms) per severity per hour " +
            "(or per user), with count of acknowledgements. Severity is 1=Critical, " +
            "2=High, 3=Medium, 4=Low. groupBy='severity' (default) or 'user'.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["groupBy"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "severity", "user" },
                    ["description"] = "Group dimension. Default 'severity'.",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            var start = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "startTime"), "startTime"));
            var end = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "endTime"), "endTime"));
            var groupBy = (Args.GetString(args, "groupBy", "severity") ?? "severity").ToLowerInvariant();

            string groupCol = groupBy == "user" ? "User_Name" : "Severity";
            if (groupBy != "user" && groupBy != "severity")
                throw new ToolException("Invalid groupBy '" + groupBy + "'.");

            var sql =
                "SELECT DATEADD(hour, DATEDIFF(hour, 0, EventTime), 0) AS Hour, " +
                "  " + groupCol + " AS " + groupCol + ", " +
                "  AVG(CAST(Alarm_UnAckDurationMs AS BIGINT)) AS AvgUnAckMs, " +
                "  COUNT(*) AS AckCount " +
                "FROM Events WHERE EventTime >= " + start + " " +
                "AND EventTime <= " + end + " AND Type = 'Alarm.Acknowledged' " +
                "GROUP BY DATEADD(hour, DATEDIFF(hour, 0, EventTime), 0), " + groupCol + " " +
                "ORDER BY Hour, " + groupCol;
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No acknowledged alarms in range.")
                : r.ToText("No acknowledged alarms in range.");
        }
    }
}
