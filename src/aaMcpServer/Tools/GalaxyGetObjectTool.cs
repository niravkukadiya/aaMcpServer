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
    /// Returns full metadata + attribute values for a single Galaxy object by tagname,
    /// via the GRAccess COM library.
    /// </summary>
    public sealed class GalaxyGetObjectTool : ITool
    {
        private readonly GalaxyClient _client;

        public GalaxyGetObjectTool(GalaxyClient client) { _client = client; }

        public string Name => "gr_get_object";

        public string Description =>
            "Get the metadata and attribute values for a single Galaxy (ArchestrA) " +
            "object by exact tagname. Returns one row per property/attribute. " +
            "Use gr_list_objects first if you don't know the exact tagname.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagname"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact Galaxy object tagname (templates are prefixed with '$').",
                },
            },
            ["required"] = new JArray { "tagname" },
        };

        public string Execute(JObject args)
        {
            var tagname = Args.GetRequiredString(args, "tagname");
            var result = _client.GetObject(tagname);
            return result.ToText("Object not found.");
        }
    }
}
