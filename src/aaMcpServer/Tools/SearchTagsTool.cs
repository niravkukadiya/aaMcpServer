// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Data.SqlClient;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    public sealed class SearchTagsTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;

        public SearchTagsTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_search_tags";

        public string Description =>
            "Search the AVEVA Historian tag dictionary by name, description or Alias (an " +
            "extended property often used as the user-friendly label). Use this first to " +
            "discover exact tagnames before calling his_get_live_values, his_query_history, " +
            "etc. Returns TagName, Alias (if configured), Description and TagType.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["query"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Text to match against TagName, Description or Alias " +
                        "(substring, case-insensitive). Omit to list.",
                },
                ["tagType"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "all", "analog", "discrete", "string" },
                    ["description"] = "Restrict by tag type. Default 'all'.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Maximum tags to return (1-500, default 50).",
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
            var query = Args.GetString(args, "query");
            var tagType = (Args.GetString(args, "tagType", "all") ?? "all").ToLowerInvariant();
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 50), 1, 500);

            var conditions = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                conditions.Add("(t.TagName LIKE @pat OR t.Description LIKE @pat OR a.PropertyValue LIKE @pat)");
                parameters.Add(new SqlParameter("@pat", "%" + query + "%"));
            }
            switch (tagType)
            {
                case "analog":   conditions.Add("t.TagName IN (SELECT TagName FROM AnalogTag)"); break;
                case "discrete": conditions.Add("t.TagName IN (SELECT TagName FROM DiscreteTag)"); break;
                case "string":   conditions.Add("t.TagName IN (SELECT TagName FROM StringTag)"); break;
                case "all": break;
                default:
                    throw new ToolException("Invalid tagType '" + tagType +
                        "'. Use all, analog, discrete or string.");
            }

            var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
            // LEFT JOIN to TagExtendedPropertyInfo for the optional Alias property. If the
            // view doesn't exist on this install the query will fail at SQL level — the
            // installer should remove this LEFT JOIN if so.
            var sql =
                "SELECT TOP (" + limit + ") t.TagName, a.PropertyValue AS Alias, " +
                "  t.Description, t.TagType " +
                "FROM Tag t " +
                "LEFT OUTER JOIN TagExtendedPropertyInfo a " +
                "  ON a.TagName = t.TagName AND a.PropertyName = 'Alias'" +
                where + " ORDER BY t.TagName";

            var result = _client.Run(sql, limit, parameters.ToArray());
            return PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(result, Name, _cfg.HistorianTimesAreUtc,
                    "No tags matched the search.")
                : result.ToText("No tags matched the search.");
        }

        internal static string PickFormat(JObject args, ServiceConfig cfg)
        {
            var v = Args.GetString(args, "format");
            if (!string.IsNullOrWhiteSpace(v))
            {
                var s = v.Trim().ToLowerInvariant();
                if (s == "table" || s == "compact") return s;
                throw new ToolException("Invalid format '" + v + "'. Use 'compact' or 'table'.");
            }
            return cfg?.OutputFormat ?? "compact";
        }
    }
}
