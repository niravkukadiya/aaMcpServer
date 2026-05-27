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
    /// <summary>Returns each value change (Delta points) for a tag over a range.</summary>
    public sealed class FindStateChangesTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public FindStateChangesTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_find_state_changes";

        public string Description =>
            "Return every value change for a tag over a range (uses Delta retrieval). " +
            "Best for discrete tags or anything whose state transitions are what you " +
            "care about. Pair with optional 'valueFilter' (e.g. '= 1') to return only " +
            "changes to a specific value.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagName"] = new JObject { ["type"] = "string", ["description"] = "Exact tagname." },
                ["startTime"] = new JObject { ["type"] = "string", ["description"] = "Range start." },
                ["endTime"] = new JObject { ["type"] = "string", ["description"] = "Range end." },
                ["valueFilter"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional value filter like '= 1', '> 0' (whitelisted ops only).",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max changes to return (default 500).",
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
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 500), 1, _cfg.MaxRows);
            var filter = Args.GetString(args, "valueFilter");
            string filterClause = "";
            if (!string.IsNullOrWhiteSpace(filter))
            {
                // Whitelist: simple "op number" form.
                var m = System.Text.RegularExpressions.Regex.Match(filter.Trim(),
                    @"^(?<op>=|<>|>=|<=|>|<)\s*(?<val>-?\d+(\.\d+)?)$");
                if (!m.Success)
                    throw new ToolException("Invalid valueFilter '" + filter +
                        "'. Use forms like '= 1', '> 0', '<= 50'.");
                filterClause = " AND Value " + m.Groups["op"].Value + " " + m.Groups["val"].Value;
            }

            var sql =
                "SELECT DateTime, TagName, vValue AS Value, Quality FROM History " +
                "WHERE TagName = " + tag + " " +
                "AND DateTime >= " + start + " AND DateTime <= " + end + " " +
                "AND wwRetrievalMode = 'Delta'" + filterClause + " " +
                "ORDER BY DateTime";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc, "No state changes found.")
                : r.ToText("No state changes found.");
        }
    }
}
