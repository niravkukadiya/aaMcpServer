// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using aaMcpServer.Historian;
using aaMcpServer.Http;
using aaMcpServer.Mcp;
using aaMcpServer.Tools;

namespace aaMcpServer
{
    /// <summary>
    /// Wires the application together: config -> data layer -> tools -> MCP server ->
    /// HTTP transport. Shared by both the console and the Windows Service hosts.
    /// New tools are registered in <see cref="Start"/>; see CreateNewTool.md.
    /// </summary>
    public sealed class AppHost
    {
        private StreamableHttpServer _http;

        public void Start()
        {
            var cfg = ServiceConfig.Load();

            Log.Configure(cfg.LogDirectory, cfg.LogFilePrefix,
                cfg.LogRotationHours, cfg.LogRetentionDays, cfg.LogMinLevel);

            Log.Info("Starting aa Mcp Server...");
            Log.Info("Logging to " + cfg.LogDirectory + " (rotation " + cfg.LogRotationHours +
                     "h, retention " + cfg.LogRetentionDays + "d).");
            Log.Info("Output format default: " + cfg.OutputFormat +
                     " (Historian.TimesAreUtc=" + cfg.HistorianTimesAreUtc + ").");
            Log.Info("Historian target: " + cfg.HistorianServer + " / " + cfg.HistorianDatabase +
                     " (user '" + cfg.HistorianUser + "')");

            var client = new HistorianClient(cfg);
            var connError = client.TestConnection();
            if (connError != null)
                Log.Warn("Historian connection test FAILED: " + connError +
                         " - the server will still start; verify the settings in the .config file.");
            else
                Log.Info("Historian connection test OK.");

            // ── Tool registry ────────────────────────────────────────────
            //  To add a tool: implement ITool, then registry.Add(new YourTool(...));
            //  See docs/CreateNewTool.md for a full walkthrough.
            var registry = new ToolRegistry();
            registry.Add(new SearchTagsTool(client, cfg));
            registry.Add(new ProbeSchemaTool(client, cfg));

            var mcp = new McpServer(registry);
            _http = new StreamableHttpServer(cfg, mcp);
            _http.Start();

            Log.Info("Server ready. Registered tools: his_search_tags, his_probe_schema.");
        }

        public void Stop()
        {
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }
            Log.Info("Server stopped.");
            Log.Shutdown();
        }
    }
}
