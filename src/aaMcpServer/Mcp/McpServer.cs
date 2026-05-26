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
    /// <summary>
    /// Implements the MCP application layer on top of JSON-RPC 2.0. It is transport
    /// agnostic: feed it a parsed JSON-RPC message and it returns the response
    /// message (or null for notifications, which require no reply).
    /// </summary>
    public sealed class McpServer
    {
        private const string ServerName = "aa-mcp-server";
        private const string ServerVersion = "1.0.0";

        // Protocol revisions we understand. We echo the client's version when we
        // support it, otherwise we offer our preferred (latest) one.
        private static readonly HashSet<string> SupportedProtocols =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "2024-11-05",
                "2025-03-26",
                "2025-06-18",
            };
        private const string PreferredProtocol = "2025-06-18";

        private readonly ToolRegistry _tools;

        public McpServer(ToolRegistry tools)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        }

        /// <summary>
        /// Handles a single JSON-RPC message. Returns the response object, or null
        /// if the message was a notification (no "id") and needs no reply.
        /// </summary>
        public JObject HandleMessage(JObject message)
        {
            if (message == null)
                return JsonRpc.Error(null, JsonRpc.InvalidRequest, "Empty request.");

            var id = message["id"]; // absent => notification
            var method = (string)message["method"];
            var prms = message["params"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(method))
                return JsonRpc.Error(id, JsonRpc.InvalidRequest, "Missing 'method'.");

            try
            {
                switch (method)
                {
                    case "initialize":
                        return JsonRpc.Result(id, Initialize(prms));

                    case "ping":
                        return JsonRpc.Result(id, new JObject());

                    case "tools/list":
                        return JsonRpc.Result(id, new JObject { ["tools"] = _tools.ToListJson() });

                    case "tools/call":
                        return JsonRpc.Result(id, CallTool(prms));

                    // Notifications (no response expected).
                    case "notifications/initialized":
                    case "notifications/cancelled":
                        return null;

                    default:
                        // Notifications for unknown methods are silently ignored.
                        if (id == null) return null;
                        return JsonRpc.Error(id, JsonRpc.MethodNotFound,
                            "Method not found: " + method);
                }
            }
            catch (ToolException tex)
            {
                // Should not normally reach here (CallTool handles it), but be safe.
                return JsonRpc.Error(id, JsonRpc.InvalidParams, tex.Message);
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled error while handling '" + method + "'", ex);
                return JsonRpc.Error(id, JsonRpc.InternalError, ex.Message);
            }
        }

        private JObject Initialize(JObject prms)
        {
            var requested = (string)prms["protocolVersion"];
            var negotiated = (requested != null && SupportedProtocols.Contains(requested))
                ? requested
                : PreferredProtocol;

            Log.Info("initialize: client requested protocol '" + (requested ?? "<none>") +
                     "', negotiated '" + negotiated + "'.");

            return new JObject
            {
                ["protocolVersion"] = negotiated,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = false },
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = ServerName,
                    ["version"] = ServerVersion,
                },
                ["instructions"] =
                    "Query an AVEVA Historian. Use his_search_tags to discover tag " +
                    "names, his_get_live_values for the latest values, his_query_history for time-series " +
                    "data with retrieval modes, and his_query_alarms_events for alarm/event history.",
            };
        }

        private JObject CallTool(JObject prms)
        {
            var name = (string)prms["name"];
            var args = prms["arguments"] as JObject;

            ITool tool;
            if (string.IsNullOrEmpty(name) || !_tools.TryGet(name, out tool))
            {
                return new JObject
                {
                    ["content"] = TextContent("Unknown tool: " + (name ?? "<null>")),
                    ["isError"] = true,
                };
            }

            try
            {
                Log.Info("tools/call '" + name + "' args=" +
                         (args != null ? args.ToString(Newtonsoft.Json.Formatting.None) : "{}"));
                var text = tool.Execute(args ?? new JObject());
                return new JObject
                {
                    ["content"] = TextContent(text),
                    ["isError"] = false,
                };
            }
            catch (ToolException tex)
            {
                return new JObject
                {
                    ["content"] = TextContent(tex.Message),
                    ["isError"] = true,
                };
            }
            catch (Exception ex)
            {
                Log.Error("Tool '" + name + "' failed", ex);
                return new JObject
                {
                    ["content"] = TextContent("Tool execution error: " + ex.Message),
                    ["isError"] = true,
                };
            }
        }

        private static JArray TextContent(string text)
        {
            return new JArray
            {
                new JObject { ["type"] = "text", ["text"] = text ?? string.Empty },
            };
        }
    }
}
