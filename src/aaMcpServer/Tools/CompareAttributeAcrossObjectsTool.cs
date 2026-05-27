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
    /// <summary>Compare the same attribute across many objects, right now (from Live).</summary>
    public sealed class CompareAttributeAcrossObjectsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public CompareAttributeAcrossObjectsTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_compare_attribute_across_objects";

        public string Description =>
            "Compare the same attribute across many objects at the current time. E.g. " +
            "'show CommunicationState of every machine right now' returns one row per " +
            "object with that attribute. Pass attributeName (e.g. 'CommunicationState') " +
            "and an optional objectPattern (substring match against object name).";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["attributeName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Attribute name (the part of the tagname AFTER the first dot).",
                },
                ["objectPattern"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional substring match against the object name.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max objects to return (1-1000, default 500).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "attributeName" },
        };

        public string Execute(JObject args)
        {
            var attr = Args.GetRequiredString(args, "attributeName").Trim();
            var objPat = Args.GetString(args, "objectPattern");
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 500), 1, 1000);
            // TagName ends with ".<attr>"
            var like = SqlSanitize.QuoteLiteral("%." + attr);
            var sql =
                "SELECT TOP (" + limit + ") TagName, " +
                "  LEFT(TagName, CHARINDEX('.', TagName) - 1) AS ObjectName, " +
                "  DateTime, vValue AS Value, Quality " +
                "FROM Live WHERE TagName LIKE " + like;
            if (!string.IsNullOrWhiteSpace(objPat))
                sql += " AND LEFT(TagName, CHARINDEX('.', TagName) - 1) LIKE " +
                       SqlSanitize.QuoteLiteral("%" + objPat + "%");
            sql += " ORDER BY TagName";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No objects have attribute '" + attr + "'.")
                : r.ToText("No objects have attribute '" + attr + "'.");
        }
    }
}
