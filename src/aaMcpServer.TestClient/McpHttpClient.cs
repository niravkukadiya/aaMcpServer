// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.TestClient
{
    /// <summary>
    /// A small MCP client over the Streamable HTTP transport: it POSTs JSON-RPC 2.0
    /// messages to the server's /mcp endpoint and returns the parsed response.
    /// Mirrors how Copilot Studio talks to the server, so it is a faithful test rig.
    /// </summary>
    public sealed class McpHttpClient : IDisposable
    {
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private int _nextId = 0;

        public string Endpoint { get; set; }

        public McpHttpClient(string endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>The last raw request body that was sent (for display).</summary>
        public string LastRequestJson { get; private set; }

        /// <summary>Sends a JSON-RPC request and returns the parsed response object.</summary>
        public async Task<JObject> SendRequestAsync(string method, JObject @params)
        {
            var id = ++_nextId;
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
            };
            if (@params != null) request["params"] = @params;

            return await PostAsync(request).ConfigureAwait(false);
        }

        /// <summary>Sends a JSON-RPC notification (no id, no response expected).</summary>
        public async Task SendNotificationAsync(string method, JObject @params = null)
        {
            var note = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
            };
            if (@params != null) note["params"] = @params;

            await PostAsync(note).ConfigureAwait(false);
        }

        private async Task<JObject> PostAsync(JObject message)
        {
            LastRequestJson = message.ToString(Formatting.Indented);

            using (var content = new StringContent(
                message.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content })
            {
                req.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");

                using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // 202 with empty body => acknowledged notification.
                    if (string.IsNullOrWhiteSpace(body))
                        return null;

                    try
                    {
                        return JObject.Parse(body);
                    }
                    catch (JsonException)
                    {
                        // Surface a non-JSON body (e.g. an error page) to the caller.
                        return new JObject
                        {
                            ["_httpStatus"] = (int)resp.StatusCode,
                            ["_rawBody"] = body,
                        };
                    }
                }
            }
        }

        // --- Convenience wrappers for the standard MCP methods ---

        public Task<JObject> InitializeAsync()
        {
            return SendRequestAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject
                {
                    ["name"] = "aa-mcp-server-test-client",
                    ["version"] = "1.0.0",
                },
            });
        }

        public Task SendInitializedAsync()
        {
            return SendNotificationAsync("notifications/initialized");
        }

        public Task<JObject> ListToolsAsync()
        {
            return SendRequestAsync("tools/list", new JObject());
        }

        public Task<JObject> CallToolAsync(string name, JObject arguments)
        {
            return SendRequestAsync("tools/call", new JObject
            {
                ["name"] = name,
                ["arguments"] = arguments ?? new JObject(),
            });
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
