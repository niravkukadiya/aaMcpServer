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
    /// <summary>Lists all historised attributes of one AVEVA object.</summary>
    public sealed class GetObjectAttributesTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetObjectAttributesTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_object_attributes";

        public string Description =>
            "List all attributes (historised tags) of a single AVEVA object. Pass the " +
            "object name (e.g. 'B0101_CONTROL'); returns TagName, AttributeName, " +
            "Description and TagType for each '<Object>.<Attribute>' tag.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact object name (the part of a tagname before the first dot).",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max attributes to return (1-2000, default 500).",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "objectName" },
        };

        public string Execute(JObject args)
        {
            var obj = Args.GetRequiredString(args, "objectName").Trim();
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 500), 1, 2000);
            var like = SqlSanitize.QuoteLiteral(obj + ".%");

            var sql =
                "SELECT TOP (" + limit + ") TagName, " +
                "  SUBSTRING(TagName, LEN(" + SqlSanitize.QuoteLiteral(obj + ".") +
                ") + 1, LEN(TagName)) AS AttributeName, " +
                "  Description, TagType " +
                "FROM Tag WHERE TagName LIKE " + like + " ORDER BY TagName";
            var r = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No attributes found for object '" + obj + "'.")
                : r.ToText("No attributes found for object '" + obj + "'.");
        }
    }
}
