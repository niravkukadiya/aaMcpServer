// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using aaMcpServer.Logging;

namespace aaMcpServer
{
    public static class Log
    {
        public const int LevelTrace = 0;
        public const int LevelDebug = 1;
        public const int LevelInfo = 2;
        public const int LevelWarn = 3;
        public const int LevelError = 4;

        private static readonly string[] LevelText = { "TRC", "DBG", "INF", "WRN", "ERR" };
        private static readonly object Gate = new object();

        private static RollingFileLogger _file;
        private static int _minLevel = LevelInfo;

        public static bool EchoToConsole { get; set; } = true;

        public static void Configure(
            string logDirectory,
            string filePrefix,
            int rotationHours,
            int retentionDays,
            int minLevel)
        {
            lock (Gate)
            {
                _minLevel = minLevel;
                var old = _file;
                var logger = new RollingFileLogger(logDirectory, filePrefix, rotationHours, retentionDays);
                logger.Start();
                _file = logger;
                if (old != null) { try { old.Dispose(); } catch { } }
            }
        }

        public static void Trace(string message) { Write(LevelTrace, message); }
        public static void Debug(string message) { Write(LevelDebug, message); }
        public static void Info(string message) { Write(LevelInfo, message); }
        public static void Warn(string message) { Write(LevelWarn, message); }

        public static void Error(string message, Exception ex = null)
        {
            if (ex != null) message += Environment.NewLine + ex;
            Write(LevelError, message);
        }

        private static void Write(int level, string message)
        {
            if (level < _minLevel) return;

            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                DateTime.Now, LevelText[level], message);

            if (EchoToConsole)
            {
                try { Console.WriteLine(line); } catch { }
            }

            RollingFileLogger file;
            lock (Gate)
            {
                if (_file == null)
                {
                    _file = new RollingFileLogger("logs", "mcp-server", 6, 7);
                    _file.Start();
                }
                file = _file;
            }
            file.WriteLine(line);
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                if (_file != null) { try { _file.Dispose(); } catch { } _file = null; }
            }
        }
    }
}
