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
    /// <summary>Current ("live") value of every attribute of one AVEVA object.</summary>
    public sealed class GetObjectSnapshotTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetObjectSnapshotTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_object_snapshot";

        public string Description =>
            "Get the current ('live') value of every attribute of a single AVEVA object " +
            "in one call. Returns AttributeName, current Value, Quality and timestamp. " +
            "Use this to answer 'what is the state of machine X right now?'.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact object name (part of a tagname before the first dot).",
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
            var like = SqlSanitize.QuoteLiteral(obj + ".%");
            var prefix = SqlSanitize.QuoteLiteral(obj + ".");

            var sql =
                "SELECT TagName, " +
                "  SUBSTRING(TagName, LEN(" + prefix + ") + 1, LEN(TagName)) AS AttributeName, " +
                "  DateTime, vValue AS Value, Quality " +
                "FROM Live WHERE TagName LIKE " + like + " ORDER BY TagName";
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(r, Name, _cfg.HistorianTimesAreUtc,
                    "No live values for object '" + obj + "'.")
                : r.ToText("No live values for object '" + obj + "'.");
        }
    }
}
