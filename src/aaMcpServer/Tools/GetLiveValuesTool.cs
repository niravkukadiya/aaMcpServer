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
    public sealed class GetLiveValuesTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;
        public GetLiveValuesTool(HistorianClient client, ServiceConfig cfg) { _client = client; _cfg = cfg; }

        public string Name => "his_get_live_values";

        public string Description =>
            "Get the latest current value and quality for one or more specific " +
            "'<Object>.<Attribute>' tags. Use when you already know exact tagnames. " +
            "For 'show me everything for one object', use his_get_object_snapshot instead.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Exact tagnames like 'B0101_CONTROL.CommunicationState'.",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format.",
                },
            },
            ["required"] = new JArray { "tagNames" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);
            var sql =
                "SELECT TagName, DateTime, vValue AS Value, Quality, QualityDetail " +
                "FROM Live WHERE TagName IN (" + inList + ") ORDER BY TagName";
            var r = _client.Run(sql, _cfg.MaxRows);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatLive(r, _cfg.HistorianTimesAreUtc,
                    "No live values found for the requested tag(s).")
                : r.ToText("No live values found for the requested tag(s).");
        }
    }
}
