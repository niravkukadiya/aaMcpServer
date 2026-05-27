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
    /// <summary>Lists distinct AVEVA objects (substring before the first dot in a tag name).</summary>
    public sealed class ListObjectsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public ListObjectsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_list_objects";

        public string Description =>
            "List distinct AVEVA System Platform objects (machines, equipment) configured " +
            "in the Historian. Each object is the substring of TagName before the first " +
            "dot — e.g. 'B0101_CONTROL' from 'B0101_CONTROL.CommunicationState'. Use this " +
            "to discover what equipment exists before drilling into a specific object's " +
            "attributes (his_get_object_attributes) or current state (his_get_object_snapshot).";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["namePattern"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Substring match against object name. Omit for all.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max objects to return (1-1000, default 200).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray(),
        };

        public string Execute(JObject args)
        {
            var pattern = Args.GetString(args, "namePattern");
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 200), 1, 1000);

            string where = "WHERE TagName LIKE '%.%' AND CHARINDEX('.', TagName) > 1";
            if (!string.IsNullOrWhiteSpace(pattern))
                where += " AND LEFT(TagName, CHARINDEX('.', TagName) - 1) LIKE " +
                         SqlSanitize.QuoteLiteral("%" + pattern + "%");

            var sql =
                "SELECT TOP (" + limit + ") " +
                "  LEFT(TagName, CHARINDEX('.', TagName) - 1) AS ObjectName, " +
                "  COUNT(*) AS AttrCount " +
                "FROM Tag " + where + " " +
                "GROUP BY LEFT(TagName, CHARINDEX('.', TagName) - 1) " +
                "ORDER BY LEFT(TagName, CHARINDEX('.', TagName) - 1)";

            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc, "No objects matched.")
                : r.ToText("No objects matched.");
        }
    }
}
