// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
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
    /// <summary>Filtered query over the Events table.</summary>
    public sealed class QueryAlarmsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public QueryAlarmsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_query_alarms";

        public string Description =>
            "Query alarms/events over a time range with optional filters: severity " +
            "(1=Critical, 2=High, 3=Medium, 4=Low), source area, alarm type, alarm state. " +
            "Returns EventTime, Type, Severity, Source_Object, Source_Area, Alarm_State, " +
            "Alarm_DurationMs, Alarm_UnAckDurationMs, ValueString, Alarm_LimitString, User_Name.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["maxSeverity"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Only severities <= this. E.g. 2 = Critical + High only.",
                },
                ["area"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional Source_Area exact match.",
                },
                ["type"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional Type filter (e.g. 'Alarm.Set', 'Alarm.Acknowledged', 'Alarm.Clear').",
                },
                ["alarmState"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional Alarm_State filter (e.g. 'UNACK_ALM').",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max records (1-5000, default 500).",
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
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 500), 1, 5000);

            var sb = new StringBuilder();
            sb.Append("SELECT TOP (").Append(limit).Append(") ");
            sb.Append("EventTime, Type, Severity, Source_Object, Source_Area, ");
            sb.Append("Alarm_State, Alarm_DurationMs, Alarm_UnAckDurationMs, ");
            sb.Append("ValueString, Alarm_LimitString, User_Name ");
            sb.Append("FROM Events WHERE EventTime >= ").Append(start);
            sb.Append(" AND EventTime <= ").Append(end);

            if (Args.HasValue(args, "maxSeverity"))
                sb.Append(" AND Severity <= ").Append(SqlSanitize.ClampInt(Args.GetInt(args, "maxSeverity", 4), 1, 4));
            var area = Args.GetString(args, "area");
            if (!string.IsNullOrWhiteSpace(area))
                sb.Append(" AND Source_Area = ").Append(SqlSanitize.QuoteLiteral(area));
            var type = Args.GetString(args, "type");
            if (!string.IsNullOrWhiteSpace(type))
                sb.Append(" AND Type = ").Append(SqlSanitize.QuoteLiteral(type));
            var state = Args.GetString(args, "alarmState");
            if (!string.IsNullOrWhiteSpace(state))
                sb.Append(" AND Alarm_State = ").Append(SqlSanitize.QuoteLiteral(state));
            sb.Append(" ORDER BY EventTime DESC");

            var r = _client.Run(sb.ToString(), limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No alarms/events in the requested range.")
                : r.ToText("No alarms/events.");
        }
    }
}
