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
    /// <summary>Simple-linear-regression forecast of a tag (wwFilter='SLR()').</summary>
    public sealed class ForecastValueTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public ForecastValueTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_forecast_value";

        public string Description =>
            "Forecast where a tag's value is heading via simple linear regression " +
            "(wwFilter='SLR()'). Pass a historical range whose trend should be extrapolated " +
            "into a future end time. Useful for 'will we hit production target by end of " +
            "shift?' style questions. Note: some Historian releases do not support SLR; " +
            "the call returns an error if so.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagName"] = new JObject { ["type"] = "string", ["description"] = "Exact tagname." },
                ["startTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Historical range start (for fitting the trend).",
                },
                ["endTime"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Range end (may be in the future to forecast past 'now').",
                },
                ["cycleCount"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of evenly spaced points (default 50).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "tagName", "startTime", "endTime" },
        };

        public string Execute(JObject args)
        {
            var tag = SqlSanitize.QuoteLiteral(Args.GetRequiredString(args, "tagName"));
            var start = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "startTime"), "startTime"));
            var end = TimeRange.ToSqlLiteral(TimeRange.Parse(Args.GetRequiredString(args, "endTime"), "endTime"));
            var cc = SqlSanitize.ClampInt(Args.GetInt(args, "cycleCount", 50), 2, 10000);

            var sql =
                "SELECT DateTime, TagName, Value FROM History " +
                "WHERE TagName = " + tag + " " +
                "AND DateTime >= " + start + " AND DateTime <= " + end + " " +
                "AND wwRetrievalMode = 'Cyclic' AND wwCycleCount = " + cc + " " +
                "AND wwFilter = 'SLR()' " +
                "ORDER BY DateTime";
            var r = _client.Run(sql, cc + 10);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatHistory(r, "Cyclic", _cfg.HistorianTimesAreUtc,
                    "No forecast data — check that the tag has enough history in the range.")
                : r.ToText("No forecast data.");
        }
    }
}
