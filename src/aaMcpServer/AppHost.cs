// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using aaMcpServer.Galaxy;
using aaMcpServer.Historian;
using aaMcpServer.Http;
using aaMcpServer.Mcp;
using aaMcpServer.Tools;

namespace aaMcpServer
{
    public sealed class AppHost
    {
        private StreamableHttpServer _http;
        private GalaxyClient _galaxy;

        public void Start()
        {
            var cfg = ServiceConfig.Load();

            Log.Configure(cfg.LogDirectory, cfg.LogFilePrefix,
                cfg.LogRotationHours, cfg.LogRetentionDays, cfg.LogMinLevel);

            Log.Info("Starting aa Mcp Server...");
            Log.Info("Logging to " + cfg.LogDirectory + " (rotation " + cfg.LogRotationHours +
                     "h, retention " + cfg.LogRetentionDays + "d).");
            Log.Info("Output format default: " + cfg.OutputFormat + " (Historian.TimesAreUtc=" +
                     cfg.HistorianTimesAreUtc + ").");
            Log.Info("Historian target: " + cfg.HistorianServer + " / " + cfg.HistorianDatabase +
                     " (user '" + cfg.HistorianUser + "')");

            var client = new HistorianClient(cfg);
            var connError = client.TestConnection();
            if (connError != null)
                Log.Warn("Historian connection test FAILED: " + connError +
                         " - the server will still start; verify the settings in the .config file.");
            else
                Log.Info("Historian connection test OK.");

            var registry = new ToolRegistry();
            registry.Add(new SearchTagsTool(client, cfg));
            registry.Add(new GetLiveValuesTool(client, cfg));
            registry.Add(new QueryHistoryTool(client, cfg));
            registry.Add(new QueryAlarmsEventsTool(client, cfg));

            if (cfg.GalaxyEnabled && !string.IsNullOrWhiteSpace(cfg.GalaxyNode) &&
                !string.IsNullOrWhiteSpace(cfg.GalaxyName))
            {
                _galaxy = new GalaxyClient(cfg);
                var galaxyErr = _galaxy.TestConnection();
                if (galaxyErr != null)
                    Log.Warn("Galaxy connection test FAILED: " + galaxyErr +
                             " - gr_* tools will return this error until resolved.");
                else
                    Log.Info("Galaxy connection OK ('" + cfg.GalaxyName + "' on '" + cfg.GalaxyNode + "').");

                registry.Add(new GalaxyListObjectsTool(_galaxy, cfg));
                registry.Add(new GalaxyGetObjectTool(_galaxy));
                Log.Info("Galaxy tools registered: gr_list_objects, gr_get_object.");
            }
            else
            {
                Log.Info("Galaxy tools NOT registered (Galaxy.Enabled is false or Node/Name missing).");
            }

            var mcp = new McpServer(registry);
            _http = new StreamableHttpServer(cfg, mcp);
            _http.Start();

            Log.Info("Server ready.");
        }

        public void Stop()
        {
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }
            if (_galaxy != null)
            {
                try { _galaxy.Dispose(); } catch { }
                _galaxy = null;
            }
            Log.Info("Server stopped.");
            Log.Shutdown();
        }
    }
}
