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
    /// An MCP tool. Each tool advertises a name, a human-readable description and
    /// a JSON Schema for its arguments, and knows how to execute a call.
    /// </summary>
    public interface ITool
    {
        /// <summary>Unique tool name (snake_case), as exposed to the model.</summary>
        string Name { get; }

        /// <summary>Natural-language description shown to the model.</summary>
        string Description { get; }

        /// <summary>JSON Schema (draft-07 style) describing the "arguments" object.</summary>
        JObject InputSchema { get; }

        /// <summary>
        /// Executes the tool. <paramref name="arguments"/> is the raw "arguments"
        /// object from the tools/call request (may be null). Returns the text the
        /// model should see. Throws <see cref="ToolException"/> for user-facing errors.
        /// </summary>
        string Execute(JObject arguments);
    }

    /// <summary>Thrown by tools to signal a user-facing error (returned as isError content).</summary>
    public sealed class ToolException : System.Exception
    {
        public ToolException(string message) : base(message) { }
    }
}
