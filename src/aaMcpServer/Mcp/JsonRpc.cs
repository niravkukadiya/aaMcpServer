// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Mcp
{
    /// <summary>
    /// Helpers for building JSON-RPC 2.0 envelopes (the wire format used by MCP).
    /// </summary>
    public static class JsonRpc
    {
        // Standard JSON-RPC 2.0 error codes.
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        /// <summary>Builds a successful result envelope for the given request id.</summary>
        public static JObject Result(JToken id, JToken result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["result"] = result ?? new JObject(),
            };
        }

        /// <summary>Builds an error envelope for the given request id.</summary>
        public static JObject Error(JToken id, int code, string message, JToken data = null)
        {
            var err = new JObject
            {
                ["code"] = code,
                ["message"] = message ?? "error",
            };
            if (data != null) err["data"] = data;

            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = err,
            };
        }
    }
}
