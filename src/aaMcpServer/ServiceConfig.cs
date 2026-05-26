// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;

namespace aaMcpServer
{
    public sealed class ServiceConfig
    {
        public string HttpPrefix { get; private set; }
        public string McpPath { get; private set; }

        public string HistorianServer { get; private set; }
        public string HistorianDatabase { get; private set; }
        public string HistorianUser { get; private set; }
        public string HistorianPassword { get; private set; }
        public int CommandTimeoutSeconds { get; private set; }
        public string ExtraConnectionOptions { get; private set; }
        public bool HistorianTimesAreUtc { get; private set; }

        public string AlarmsSource { get; private set; }
        public string AlarmsTimeColumn { get; private set; }

        public int MaxRows { get; private set; }

        public string OutputFormat { get; private set; }   // "compact" | "table"

        public string LogDirectory { get; private set; }
        public string LogFilePrefix { get; private set; }
        public int LogRotationHours { get; private set; }
        public int LogRetentionDays { get; private set; }
        public int LogMinLevel { get; private set; }

        public bool GalaxyEnabled { get; private set; }
        public string GalaxyNode { get; private set; }
        public string GalaxyName { get; private set; }
        public string GalaxyUser { get; private set; }
        public string GalaxyPassword { get; private set; }
        public string[] GalaxyProgIds { get; private set; }

        public static ServiceConfig Load()
        {
            var cfg = new ServiceConfig
            {
                HttpPrefix = Get("Http.Prefix", "http://+:8080/"),
                McpPath = Get("Http.McpPath", "/mcp"),

                HistorianServer = Get("Historian.Server", "localhost"),
                HistorianDatabase = Get("Historian.Database", "Runtime"),
                HistorianUser = Get("Historian.User", ""),
                HistorianPassword = Get("Historian.Password", ""),
                CommandTimeoutSeconds = GetInt("Historian.CommandTimeoutSeconds", 60),
                ExtraConnectionOptions = Get("Historian.ExtraConnectionOptions", ""),
                HistorianTimesAreUtc = GetBool("Historian.TimesAreUtc", false),

                AlarmsSource = Get("Alarms.Source", "v_AlarmEventHistory"),
                AlarmsTimeColumn = Get("Alarms.TimeColumn", "EventStamp"),

                MaxRows = GetInt("Limits.MaxRows", 5000),

                OutputFormat = NormaliseFormat(Get("Output.Format", "compact")),

                LogDirectory = Get("Log.Directory", "logs"),
                LogFilePrefix = Get("Log.FilePrefix", "mcp-server"),
                LogRotationHours = GetInt("Log.RotationHours", 6),
                LogRetentionDays = GetInt("Log.RetentionDays", 7),
                LogMinLevel = ParseLevel(Get("Log.MinLevel", "INFO")),

                GalaxyEnabled = GetBool("Galaxy.Enabled", false),
                GalaxyNode = Get("Galaxy.Node", ""),
                GalaxyName = Get("Galaxy.Name", ""),
                GalaxyUser = Get("Galaxy.User", ""),
                GalaxyPassword = Get("Galaxy.Password", ""),
                GalaxyProgIds = SplitCsv(Get("Galaxy.ProgIds",
                    "ArchestrA.GRAccessApp,ArchestrA.GRAccessApp.4,ArchestrA.GRAccessApp.5,ArchestrA.GRAccessApp.6")),
            };

            if (!Path.IsPathRooted(cfg.LogDirectory))
                cfg.LogDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, cfg.LogDirectory);

            if (!cfg.HttpPrefix.EndsWith("/", StringComparison.Ordinal))
                cfg.HttpPrefix += "/";

            if (!cfg.McpPath.StartsWith("/", StringComparison.Ordinal))
                cfg.McpPath = "/" + cfg.McpPath;
            cfg.McpPath = cfg.McpPath.TrimEnd('/');
            if (cfg.McpPath.Length == 0) cfg.McpPath = "/";

            return cfg;
        }

        public string BuildConnectionString()
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = HistorianServer,
                InitialCatalog = HistorianDatabase,
                UserID = HistorianUser,
                Password = HistorianPassword,
                IntegratedSecurity = false,
                ConnectTimeout = 30,
            };
            var cs = b.ConnectionString;
            if (!string.IsNullOrWhiteSpace(ExtraConnectionOptions))
            {
                if (!cs.EndsWith(";", StringComparison.Ordinal)) cs += ";";
                cs += ExtraConnectionOptions;
            }
            return cs;
        }

        private static string Get(string key, string fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        private static int GetInt(string key, int fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            int parsed;
            return int.TryParse(v, out parsed) ? parsed : fallback;
        }

        private static bool GetBool(string key, bool fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            bool parsed;
            return bool.TryParse(v, out parsed) ? parsed : fallback;
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            var parts = value.Split(',');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        private static string NormaliseFormat(string value)
        {
            var v = (value ?? "compact").Trim().ToLowerInvariant();
            return (v == "table") ? "table" : "compact";
        }

        private static int ParseLevel(string level)
        {
            switch ((level ?? "").Trim().ToUpperInvariant())
            {
                case "TRACE": return Log.LevelTrace;
                case "DEBUG": return Log.LevelDebug;
                case "WARN": return Log.LevelWarn;
                case "ERROR": return Log.LevelError;
                default: return Log.LevelInfo;
            }
        }
    }
}
