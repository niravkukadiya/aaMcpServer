// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>Returns the value of each tag at (or bounding) a specific moment in time.</summary>
    public sealed class GetValueAtTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetValueAtTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_value_at";

        public string Description =>
            "Return the value of one or more tags AT a specific moment in time (uses " +
            "the AVEVA Historian Bounding-Value retrieval mode). 'time' accepts " +
            "ISO/SQL datetime, 'now', 'today', 'yesterday', 'last 1h', '30m ago', etc. " +
            "By default returns the last value at-or-before the time (StartBound); " +
            "pass bound='end' to return the first value after the time.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Exact tagnames.",
                },
                ["time"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Datetime or shorthand ('now', 'today', 'last 1h', ISO, etc.).",
                },
                ["bound"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "start", "end" },
                    ["description"] = "'start' = last value at-or-before time (default); 'end' = first value after.",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "tagNames", "time" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);
            var t = TimeRange.Parse(Args.GetRequiredString(args, "time"), "time");
            var ts = TimeRange.ToSqlLiteral(t);
            var bound = (Args.GetString(args, "bound", "start") ?? "start").ToLowerInvariant();
            var mode = bound == "end" ? "EndBound" : "StartBound";

            var sql =
                "SELECT DateTime, TagName, vValue AS Value, Quality, QualityDetail " +
                "FROM History WHERE TagName IN (" + inList + ") " +
                "AND DateTime >= " + ts + " " +
                "AND wwRetrievalMode = '" + mode + "'";

            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No values bounding " + t.ToString("yyyy-MM-dd HH:mm:ss") + " for the requested tag(s).")
                : r.ToText("No values bounding the requested moment.");
        }
    }
}
