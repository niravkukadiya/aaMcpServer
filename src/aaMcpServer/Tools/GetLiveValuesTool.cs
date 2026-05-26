// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
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

        public GetLiveValuesTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_get_live_values";

        public string Description =>
            "Get the latest current value and quality for one or more tags from the AVEVA " +
            "Historian Live table. Output defaults to a compact, token-efficient format with " +
            "epoch_ms timestamps (constant quality is factored to the header).";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "One or more exact tag names to read the current value for.",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = server's Output.Format ('compact').",
                },
            },
            ["required"] = new JArray { "tagNames" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);
            var sql = "SELECT TagName, DateTime, vValue AS Value, Quality, QualityDetail " +
                      "FROM Live WHERE TagName IN (" + inList + ") ORDER BY TagName";
            var result = _client.Run(sql, _cfg.MaxRows);

            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatLive(result, _cfg.HistorianTimesAreUtc,
                    "No live values found for the requested tag(s).")
                : result.ToText("No live values found for the requested tag(s).");
        }
    }
}
