// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
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
    /// Wires the application together: config → data layer → tools → MCP server →
    /// HTTP transport. New tools are registered in <see cref="Start"/>; see
    /// docs/CreateNewTool.md for the recipe.
    /// </summary>
    public sealed class AppHost
    {
        private StreamableHttpServer _http;

        public void Start()
        {
            var cfg = ServiceConfig.Load();

            Log.Configure(cfg.LogDirectory, cfg.LogFilePrefix,
                cfg.LogRotationHours, cfg.LogRetentionDays, cfg.LogMinLevel);

            Log.Info("Starting aa Mcp Server v1.4...");
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

            // Diagnostic
            registry.Add(new ProbeSchemaTool(client, cfg));

            // Tier 1 — object & tag discovery / current value
            registry.Add(new SearchTagsTool(client, cfg));
            registry.Add(new ListObjectsTool(client, cfg));
            registry.Add(new GetObjectAttributesTool(client, cfg));
            registry.Add(new GetObjectSnapshotTool(client, cfg));
            registry.Add(new GetLiveValuesTool(client, cfg));
            registry.Add(new GetValueAtTool(client, cfg));
            registry.Add(new QueryHistoryTool(client, cfg));

            // Tier 2 — summaries
            registry.Add(new GetSummaryTool(client, cfg));
            registry.Add(new GetStateSummaryTool(client, cfg));

            // Tier 3 — detection / cross-equipment
            registry.Add(new FindThresholdCrossingsTool(client, cfg));
            registry.Add(new FindStateChangesTool(client, cfg));
            registry.Add(new CompareAttributeAcrossObjectsTool(client, cfg));

            // Tier 4 — alarms / events
            registry.Add(new QueryAlarmsTool(client, cfg));
            registry.Add(new ObjectAlarmHistoryTool(client, cfg));
            registry.Add(new GetAlarmTimelineTool(client, cfg));
            registry.Add(new AlarmStatsTool(client, cfg));
            registry.Add(new ResponseMetricsTool(client, cfg));

            // Tier 5 — predictive
            registry.Add(new ForecastValueTool(client, cfg));

            var mcp = new McpServer(registry);
            _http = new StreamableHttpServer(cfg, mcp);
            _http.Start();

            Log.Info("Server ready. 19 tools registered (1 diagnostic + 18 his_*).");
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
