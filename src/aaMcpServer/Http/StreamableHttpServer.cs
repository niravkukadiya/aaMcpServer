// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using aaMcpServer.Mcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Http
{
    /// <summary>
    /// A minimal implementation of the MCP "Streamable HTTP" transport on top of
    /// <see cref="HttpListener"/> (so it runs on .NET Framework 4.8 with no ASP.NET
    /// Core dependency). It accepts JSON-RPC requests via HTTP POST to the MCP path
    /// and returns the JSON-RPC response as a single application/json body.
    ///
    /// This is the transport Microsoft Copilot Studio expects when a custom
    /// connector is marked with x-ms-agentic-protocol: mcp-streamable-1.0.
    /// </summary>
    public sealed class StreamableHttpServer
    {
        private readonly ServiceConfig _cfg;
        private readonly McpServer _mcp;
        private readonly HttpListener _listener = new HttpListener();
        private Thread _acceptThread;
        private volatile bool _running;

        public StreamableHttpServer(ServiceConfig cfg, McpServer mcp)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _mcp = mcp ?? throw new ArgumentNullException(nameof(mcp));
            _listener.Prefixes.Add(_cfg.HttpPrefix);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "mcp-http-accept" };
            _acceptThread.Start();
            Log.Info("HTTP listener started on " + _cfg.HttpPrefix +
                     " (MCP endpoint: " + _cfg.McpPath + ")");
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            Log.Info("HTTP listener stopped.");
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext(); // blocks until a request arrives
                }
                catch (Exception) when (!_running)
                {
                    break; // listener was stopped
                }
                catch (Exception ex)
                {
                    Log.Error("Accept loop error", ex);
                    continue;
                }

                ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
            }
        }

        private void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var path = (req.Url.AbsolutePath ?? "/").TrimEnd('/');
                if (path.Length == 0) path = "/";
                var mcpPath = _cfg.McpPath;

                AddCommonHeaders(ctx.Response);

                // CORS pre-flight.
                if (req.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                // Health/info endpoint at the root.
                if (path == "/" && req.HttpMethod == "GET")
                {
                    WriteText(ctx, 200, "aa Mcp Server is running. " +
                        "POST JSON-RPC to " + mcpPath + ".");
                    return;
                }

                // Everything else must target the configured MCP path.
                if (!string.Equals(path, mcpPath, StringComparison.OrdinalIgnoreCase))
                {
                    WriteText(ctx, 404, "Not found.");
                    return;
                }

                // We do not offer a server-initiated SSE stream, so reject GET on /mcp.
                if (req.HttpMethod == "GET")
                {
                    ctx.Response.AddHeader("Allow", "POST, OPTIONS");
                    WriteText(ctx, 405, "Method Not Allowed. Use POST for JSON-RPC.");
                    return;
                }

                if (req.HttpMethod != "POST")
                {
                    ctx.Response.AddHeader("Allow", "POST, OPTIONS");
                    WriteText(ctx, 405, "Method Not Allowed.");
                    return;
                }

                HandleMcpPost(ctx);
            }
            catch (Exception ex)
            {
                Log.Error("Request handling error", ex);
                try { WriteJson(ctx, 200, JsonRpc.Error(null, JsonRpc.InternalError, ex.Message)); }
                catch { }
            }
        }

        private void HandleMcpPost(HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(
                ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            JToken parsed;
            try
            {
                parsed = string.IsNullOrWhiteSpace(body) ? null : JToken.Parse(body);
            }
            catch (JsonException jex)
            {
                WriteJson(ctx, 200, JsonRpc.Error(null, JsonRpc.ParseError,
                    "Invalid JSON: " + jex.Message));
                return;
            }

            if (parsed == null)
            {
                WriteJson(ctx, 200, JsonRpc.Error(null, JsonRpc.InvalidRequest, "Empty body."));
                return;
            }

            // A JSON-RPC payload may be a single object or a batch array.
            if (parsed.Type == JTokenType.Array)
            {
                var responses = new JArray();
                foreach (var item in (JArray)parsed)
                {
                    var resp = _mcp.HandleMessage(item as JObject);
                    if (resp != null) responses.Add(resp);
                }

                if (responses.Count == 0)
                {
                    // The batch was entirely notifications => 202 with no body.
                    ctx.Response.StatusCode = 202;
                    ctx.Response.Close();
                    return;
                }
                WriteJson(ctx, 200, responses);
                return;
            }

            var single = _mcp.HandleMessage(parsed as JObject);
            if (single == null)
            {
                // Notification => acknowledge with 202 and no body.
                ctx.Response.StatusCode = 202;
                ctx.Response.Close();
                return;
            }

            WriteJson(ctx, 200, single);
        }

        private static void AddCommonHeaders(HttpListenerResponse res)
        {
            // Permissive CORS so browser-based MCP inspectors can connect too.
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers",
                "Content-Type, Accept, Mcp-Session-Id, MCP-Protocol-Version");
        }

        private static void WriteJson(HttpListenerContext ctx, int status, JToken payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        private static void WriteText(HttpListenerContext ctx, int status, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
    }
}
