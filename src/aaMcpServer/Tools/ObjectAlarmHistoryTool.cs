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
    /// <summary>All alarm/event records for one Source_Object.</summary>
    public sealed class ObjectAlarmHistoryTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public ObjectAlarmHistoryTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_object_alarm_history";

        public string Description =>
            "All alarm/event records for a single AVEVA object over a time range. " +
            "Returns the lifecycle of every alarm on that object in chronological order.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact Source_Object value (matches Source_Object column).",
                },
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max records (default 500).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "objectName", "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            var obj = Args.GetRequiredString(args, "objectName");
            var start = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "startTime"), "startTime"));
            var end = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "endTime"), "endTime"));
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 500), 1, 5000);

            var sql =
                "SELECT TOP (" + limit + ") EventTime, Type, Severity, Source_ConditionVariable, " +
                "  Alarm_State, Alarm_DurationMs, Alarm_UnAckDurationMs, " +
                "  ValueString, Alarm_LimitString, User_Name " +
                "FROM Events WHERE Source_Object = " + SqlSanitize.QuoteLiteral(obj) + " " +
                "AND EventTime >= " + start + " AND EventTime <= " + end + " " +
                "ORDER BY EventTime DESC";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No alarms for '" + obj + "' in the range.")
                : r.ToText("No alarms for '" + obj + "' in the range.");
        }
    }
}
