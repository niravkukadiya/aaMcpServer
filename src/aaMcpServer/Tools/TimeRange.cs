// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using aaMcpServer.Mcp;

namespace aaMcpServer.Tools
{
    /// <summary>
    /// Parses friendly time-range expressions that LLMs tend to send:
    ///   "now", "today", "yesterday"
    ///   "last 1h", "last 24h", "last 7d", "last 30m", "last 90s"
    ///   "1h ago", "30m ago", "2d ago"
    ///   plus any ISO-8601 or SQL-style datetime DateTime.Parse handles.
    /// All results are in local time (matching how the Historian stores/returns).
    /// </summary>
    internal static class TimeRange
    {
        private static readonly Regex RelPattern = new Regex(
            @"^(?:last\s+|past\s+)?(?<n>\d+)\s*(?<u>s|sec|secs|seconds?|m|min|mins|minutes?|h|hr|hrs|hours?|d|day|days|w|wk|wks|weeks?)(?:\s+ago)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static DateTime Parse(string value, string argName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ToolException("Missing " + argName + ".");
            var v = value.Trim();
            var lower = v.ToLowerInvariant();
            var now = DateTime.Now;

            if (lower == "now") return now;
            if (lower == "today" || lower == "start of today") return now.Date;
            if (lower == "end of today" || lower == "today end") return now.Date.AddDays(1).AddTicks(-1);
            if (lower == "yesterday" || lower == "start of yesterday") return now.Date.AddDays(-1);
            if (lower == "end of yesterday") return now.Date.AddTicks(-1);

            var m = RelPattern.Match(lower);
            if (m.Success)
            {
                int n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
                var u = m.Groups["u"].Value;
                return now - UnitToSpan(n, u);
            }

            DateTime dt;
            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                return dt;
            if (DateTime.TryParse(v, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
                return dt;
            throw new ToolException(
                "Could not parse " + argName + " '" + value + "'. Use 'now', 'today', " +
                "'yesterday', 'last 1h', '30m ago', or an ISO/SQL datetime like " +
                "'2026-05-27 14:32:00'.");
        }

        /// <summary>Format a DateTime for the Historian's quoted SQL literal (yyyy-MM-dd HH:mm:ss.fff).</summary>
        public static string ToSqlLiteral(DateTime t)
        {
            return "'" + t.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";
        }

        private static TimeSpan UnitToSpan(int n, string u)
        {
            u = u.ToLowerInvariant();
            if (u.StartsWith("s")) return TimeSpan.FromSeconds(n);
            if (u.StartsWith("m") && !u.StartsWith("min") == false) return TimeSpan.FromMinutes(n);
            if (u == "m" || u.StartsWith("min")) return TimeSpan.FromMinutes(n);
            if (u.StartsWith("h")) return TimeSpan.FromHours(n);
            if (u.StartsWith("d")) return TimeSpan.FromDays(n);
            if (u.StartsWith("w")) return TimeSpan.FromDays(7 * n);
            throw new ToolException("Unknown time unit '" + u + "'.");
        }
    }
}
