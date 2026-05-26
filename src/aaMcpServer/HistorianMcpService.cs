// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.ServiceProcess;

namespace aaMcpServer
{
    /// <summary>
    /// Windows Service wrapper around <see cref="AppHost"/>. The service name must
    /// match the name used when installing the service (see Program.cs --install).
    /// </summary>
    public sealed class HistorianMcpService : ServiceBase
    {
        public const string SvcName = "aaMcpServer";

        private AppHost _host;

        public HistorianMcpService()
        {
            ServiceName = SvcName;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            Log.EchoToConsole = false;
            _host = new AppHost();
            _host.Start();
        }

        protected override void OnStop()
        {
            _host?.Stop();
        }

        protected override void OnShutdown()
        {
            _host?.Stop();
        }
    }
}
