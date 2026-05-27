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
    /// <summary>Full raise → acknowledge → clear timeline for one alarm.</summary>
    public sealed class GetAlarmTimelineTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetAlarmTimelineTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_alarm_timeline";

        public string Description =>
            "Return the full Set → Acknowledged → Clear timeline for alarms in a range. " +
            "Uses a self-join on Alarm_ID to pair Set with Ack/Clear so each row shows the " +
            "complete lifecycle including SecsInAlarm and SecsUnAck. Useful for " +
            "operator-response analysis.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["objectName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional Source_Object filter.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max rows (default 500).",
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
            var objFilter = Args.GetString(args, "objectName");

            var objClause = string.IsNullOrWhiteSpace(objFilter)
                ? ""
                : " AND e.Source_Object = " + SqlSanitize.QuoteLiteral(objFilter);

            var sql =
                "SELECT TOP (" + limit + ") " +
                "  e.EventTime AS AlarmTime, e.Source_Object AS ObjectName, " +
                "  e.Severity, e.Alarm_Type, e.ValueString, e.Alarm_LimitString, " +
                "  a.EventTime AS AckTime, c.EventTime AS ClearTime, " +
                "  CAST(a.Alarm_UnAckDurationMs / 1000.0 AS DECIMAL(18,2)) AS SecsUnAck, " +
                "  CAST(c.Alarm_DurationMs / 1000.0 AS DECIMAL(18,2)) AS SecsInAlarm, " +
                "  a.User_Name AS AckedBy " +
                "FROM Events e " +
                "LEFT OUTER JOIN Events a " +
                "  ON a.Alarm_ID = e.Alarm_ID AND a.Type = 'Alarm.Acknowledged' " +
                "  AND a.EventTime >= " + start + " AND a.EventTime <= " + end + " " +
                "LEFT OUTER JOIN Events c " +
                "  ON c.Alarm_ID = e.Alarm_ID AND c.Type = 'Alarm.Clear' " +
                "  AND c.EventTime >= " + start + " AND c.EventTime <= " + end + " " +
                "WHERE e.EventTime >= " + start + " AND e.EventTime <= " + end + " " +
                "AND e.Type = 'Alarm.Set'" + objClause + " " +
                "ORDER BY e.EventTime DESC";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc, "No alarms in range.")
                : r.ToText("No alarms in range.");
        }
    }
}
