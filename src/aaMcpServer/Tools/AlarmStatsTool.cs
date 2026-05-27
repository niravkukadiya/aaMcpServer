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
    /// <summary>Most frequent alarms grouped by Source_Object + Source_ConditionVariable.</summary>
    public sealed class AlarmStatsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public AlarmStatsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_alarm_stats";

        public string Description =>
            "Most-frequent-alarms ranking over a time range. Groups Alarm.Set rows by " +
            "Source_Object + Source_ConditionVariable and counts occurrences. Use to " +
            "answer 'which alarm fires the most?' or 'which equipment is noisiest?'.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max groups to return (default 50).",
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
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 50), 1, 1000);

            var sql =
                "SELECT TOP (" + limit + ") Source_Object, Source_ConditionVariable, " +
                "  COUNT(*) AS AlarmCount, " +
                "  MAX(EventTime) AS LastSeen " +
                "FROM Events WHERE EventTime >= " + start + " " +
                "AND EventTime <= " + end + " AND Type = 'Alarm.Set' " +
                "GROUP BY Source_Object, Source_ConditionVariable " +
                "ORDER BY COUNT(*) DESC";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc, "No alarms in range.")
                : r.ToText("No alarms in range.");
        }
    }
}
