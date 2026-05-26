// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Mcp
{
    /// <summary>Holds the set of tools the server exposes, keyed by name.</summary>
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools =
            new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ITool> _ordered = new List<ITool>();

        public void Add(ITool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
            _ordered.Add(tool);
        }

        public bool TryGet(string name, out ITool tool)
        {
            tool = null;
            return name != null && _tools.TryGetValue(name, out tool);
        }

        /// <summary>Builds the JSON array used by the tools/list response.</summary>
        public JArray ToListJson()
        {
            var arr = new JArray();
            foreach (var t in _ordered)
            {
                arr.Add(new JObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["inputSchema"] = t.InputSchema,
                });
            }
            return arr;
        }
    }
}
