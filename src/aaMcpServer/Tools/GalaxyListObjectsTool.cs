// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using aaMcpServer.Galaxy;
using aaMcpServer.Mcp;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>
    /// Lists Galaxy objects (templates or instances) matching an optional name pattern.
    /// Uses the GRAccess COM library on the host machine.
    /// </summary>
    public sealed class GalaxyListObjectsTool : ITool
    {
        private readonly GalaxyClient _client;
        private readonly ServiceConfig _cfg;

        public GalaxyListObjectsTool(GalaxyClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "gr_list_objects";

        public string Description =>
            "List objects in the configured AVEVA / Wonderware Galaxy (ArchestrA) by " +
            "type (templates or instances) and an optional name pattern. Returns tag name, " +
            "the template the object derives from, config version and contained-name. " +
            "Use this to discover Galaxy objects before calling gr_get_object.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "templates", "instances" },
                    ["description"] = "Which kind of objects to return. Default 'instances'.",
                },
                ["namePattern"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Wildcard pattern matched against the tagname " +
                        "(GRAccess style, e.g. '*Pump*'). Omit for all.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Maximum number of objects to return (1-2000, default 200).",
                },
            },
            ["required"] = new JArray(),
        };

        public string Execute(JObject args)
        {
            var kind = (Args.GetString(args, "type", "instances") ?? "instances").ToLowerInvariant();
            bool templatesOnly;
            switch (kind)
            {
                case "templates": templatesOnly = true; break;
                case "instances": templatesOnly = false; break;
                default:
                    throw new ToolException("Invalid type '" + kind + "'. Use 'templates' or 'instances'.");
            }

            var pattern = Args.GetString(args, "namePattern");
            var limit = Historian.SqlSanitize.ClampInt(Args.GetInt(args, "limit", 200), 1, 2000);

            var result = _client.ListObjects(templatesOnly, pattern, limit);
            return result.ToText("No Galaxy objects matched.");
        }
    }
}
