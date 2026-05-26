// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace aaMcpServer
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Service management helpers (require an elevated prompt).
            if (HasFlag(args, "--install"))   return InstallService();
            if (HasFlag(args, "--uninstall")) return UninstallService();
            if (HasFlag(args, "--help") || HasFlag(args, "-h")) { PrintUsage(); return 0; }

            var runConsole = HasFlag(args, "--console") || Environment.UserInteractive;

            if (runConsole)
                return RunConsole();

            // Running under the Service Control Manager.
            ServiceBase.Run(new ServiceBase[] { new HistorianMcpService() });
            return 0;
        }

        private static int RunConsole()
        {
            Log.EchoToConsole = true;
            var host = new AppHost();

            try
            {
                host.Start();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start.", ex);
                return 1;
            }

            var stop = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true; // let us shut down gracefully
                stop.Set();
            };

            Console.WriteLine();
            Console.WriteLine("aa Mcp Server is running. Press Ctrl+C to stop.");
            stop.Wait();

            host.Stop();
            return 0;
        }

        // ---- service install/uninstall via sc.exe (keeps the project dependency-free) ----

        private static int InstallService()
        {
            var exe = Assembly.GetExecutingAssembly().Location;
            var rc = RunSc("create " + HistorianMcpService.SvcName +
                           " binPath= \"" + exe + "\" start= auto " +
                           "DisplayName= \"aa Mcp Server\"");
            if (rc == 0)
            {
                RunSc("description " + HistorianMcpService.SvcName +
                      " \"Exposes the AVEVA Historian to Microsoft Copilot via MCP over Streamable HTTP.\"");
                Console.WriteLine("Service installed. Start it with:  net start " +
                                  HistorianMcpService.SvcName);
            }
            return rc;
        }

        private static int UninstallService()
        {
            RunSc("stop " + HistorianMcpService.SvcName);
            return RunSc("delete " + HistorianMcpService.SvcName);
        }

        private static int RunSc(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    var output = p.StandardOutput.ReadToEnd();
                    var error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (!string.IsNullOrWhiteSpace(output)) Console.WriteLine(output.Trim());
                    if (!string.IsNullOrWhiteSpace(error)) Console.Error.WriteLine(error.Trim());
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to run sc.exe (run as Administrator): " + ex.Message);
                return 1;
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            return args != null && args.Any(a =>
                string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintUsage()
        {
            Console.WriteLine("aa Mcp Server");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  aaMcpServer.exe              Run as a Windows Service (under SCM)");
            Console.WriteLine("  aaMcpServer.exe --console    Run in the foreground (debugging)");
            Console.WriteLine("  aaMcpServer.exe --install    Install the Windows Service (admin)");
            Console.WriteLine("  aaMcpServer.exe --uninstall  Remove the Windows Service (admin)");
        }
    }
}
