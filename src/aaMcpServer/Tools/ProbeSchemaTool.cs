// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>
    /// Runs a battery of small probe queries against the Historian to verify
    /// that the tables, views, columns and retrieval modes that the rest of the
    /// MCP tools assume actually exist on THIS install. Returns a single text
    /// report — one line per probe — designed for paste-back into chat for
    /// diagnosis before building out the full tool surface.
    ///
    /// Each probe catches its own SqlException so one missing object doesn't
    /// hide the rest.
    /// </summary>
    public sealed class ProbeSchemaTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;

        public ProbeSchemaTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_probe_schema";

        public string Description =>
            "Diagnostic: runs ~15 small probe queries to verify which Historian tables, " +
            "views, columns and retrieval modes are available on this server. Returns a " +
            "single text report (OK / FAIL per probe). Use this once after connecting to " +
            "a new Historian to confirm the assumptions other tools rely on. Takes no " +
            "required arguments.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["verbose"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "If true, include sample row data per probe. Default true.",
                },
            },
            ["required"] = new JArray(),
        };

        public string Execute(JObject args)
        {
            bool verbose = true;
            var v = args?["verbose"];
            if (v != null && v.Type == JTokenType.Boolean) verbose = (bool)v;

            var sb = new StringBuilder();
            sb.AppendLine("# his_probe_schema report");
            sb.AppendLine("# Server: " + _cfg.HistorianServer + " / " + _cfg.HistorianDatabase);
            sb.AppendLine("# Run at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();

            int passed = 0, failed = 0;

            // 1. Base connection
            passed += Probe(sb, "0. Connection",
                "SELECT @@VERSION AS V",
                row => verbose ? "SQL Server: " + Truncate((string)row[0], 60) : null,
                ref failed);

            // ── Tag dictionary tables ────────────────────────────────────
            passed += Probe(sb, "1. Tag table",
                "SELECT COUNT(*) AS n FROM Tag",
                row => "rows: " + row[0],
                ref failed);

            passed += Probe(sb, "2. Tag columns (Description, TagType)",
                "SELECT TOP 1 TagName, Description, TagType FROM Tag",
                row => verbose ? "sample: " + row[0] + " | " + Truncate(NullSafe(row[1]), 30) +
                                 " | TagType=" + row[2] : "ok",
                ref failed);

            passed += Probe(sb, "3. AnalogTag",
                "SELECT COUNT(*) AS n FROM AnalogTag",
                row => "rows: " + row[0],
                ref failed);

            passed += Probe(sb, "4. DiscreteTag",
                "SELECT COUNT(*) AS n FROM DiscreteTag",
                row => "rows: " + row[0],
                ref failed);

            passed += Probe(sb, "5. StringTag",
                "SELECT COUNT(*) AS n FROM StringTag",
                row => "rows: " + row[0],
                ref failed);

            // ── Live table ────────────────────────────────────────────────
            passed += Probe(sb, "6. Live table",
                "SELECT TOP 1 TagName, DateTime, vValue, Quality FROM Live",
                row => verbose ? "sample: " + row[0] + " @ " + row[1] +
                                 " = " + NullSafe(row[2]) + " (Q=" + row[3] + ")" : "ok",
                ref failed);

            // ── History + retrieval modes ─────────────────────────────────
            passed += Probe(sb, "7. History basic (no ww params)",
                "SELECT TOP 1 DateTime, TagName, vValue FROM History " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM AnalogTag) " +
                "AND DateTime >= DATEADD(HOUR, -24, GETDATE())",
                row => verbose ? "sample: " + row[0] + " " + row[1] + " = " + NullSafe(row[2]) : "ok",
                ref failed);

            passed += Probe(sb, "8. wwRetrievalMode='Cyclic' + wwCycleCount",
                "SELECT TOP 1 DateTime, TagName, vValue FROM History " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM AnalogTag) " +
                "AND DateTime >= DATEADD(HOUR, -24, GETDATE()) AND DateTime <= GETDATE() " +
                "AND wwRetrievalMode = 'Cyclic' AND wwCycleCount = 1",
                row => "ok",
                ref failed);

            passed += Probe(sb, "9. wwRetrievalMode='Delta'",
                "SELECT TOP 1 DateTime, TagName, vValue FROM History " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM AnalogTag) " +
                "AND DateTime >= DATEADD(HOUR, -24, GETDATE()) AND DateTime <= GETDATE() " +
                "AND wwRetrievalMode = 'Delta'",
                row => "ok",
                ref failed);

            passed += Probe(sb, "10. wwRetrievalMode='StartBound' (for his_get_value_at)",
                "SELECT TOP 1 DateTime, TagName, vValue FROM History " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM AnalogTag) " +
                "AND DateTime >= DATEADD(MINUTE, -1, GETDATE()) " +
                "AND wwRetrievalMode = 'StartBound'",
                row => "ok",
                ref failed);

            // ── Summary views ────────────────────────────────────────────
            passed += Probe(sb, "11. AnalogSummaryHistory view + Min/Max/Avg cols",
                "SELECT TOP 1 TagName, Minimum, Maximum, Average FROM AnalogSummaryHistory " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM AnalogTag) " +
                "AND StartDateTime >= DATEADD(HOUR, -1, GETDATE()) " +
                "AND EndDateTime <= GETDATE() AND wwCycleCount = 1",
                row => verbose ? "sample: " + row[0] + " min=" + NullSafe(row[1]) +
                                 " max=" + NullSafe(row[2]) + " avg=" + NullSafe(row[3]) : "ok",
                ref failed);

            passed += Probe(sb, "12. StateSummaryHistory view + StateCount/StateTimeTotal",
                "SELECT TOP 1 TagName, Value, StateCount, StateTimeTotal " +
                "FROM StateSummaryHistory " +
                "WHERE TagName IN (SELECT TOP 1 TagName FROM DiscreteTag) " +
                "AND StartDateTime >= DATEADD(HOUR, -1, GETDATE()) " +
                "AND EndDateTime <= GETDATE() AND wwCycleCount = 1",
                row => verbose ? "sample: " + row[0] + " val=" + NullSafe(row[1]) +
                                 " count=" + NullSafe(row[2]) + " total=" + NullSafe(row[3]) + "ms" : "ok",
                ref failed);

            // ── Events / alarms ──────────────────────────────────────────
            passed += Probe(sb, "13. Events table (alarms)",
                "SELECT TOP 1 EventTime, Type, Severity, Source_Object FROM Events " +
                "WHERE EventTime >= DATEADD(HOUR, -168, GETDATE())",
                row => verbose ? "sample: " + row[0] + " " + NullSafe(row[1]) +
                                 " sev=" + NullSafe(row[2]) + " obj=" + NullSafe(row[3]) : "ok",
                ref failed);

            passed += Probe(sb, "14. Events extended columns (Alarm_State, Alarm_DurationMs, User_Name)",
                "SELECT TOP 1 Alarm_State, Alarm_DurationMs, User_Name FROM Events " +
                "WHERE EventTime >= DATEADD(HOUR, -168, GETDATE())",
                row => "ok",
                ref failed);

            // ── Extended properties ──────────────────────────────────────
            passed += Probe(sb, "15. TagExtendedPropertyInfo (Alias support)",
                "SELECT TOP 1 TagName, PropertyName, PropertyValue FROM TagExtendedPropertyInfo",
                row => verbose ? "sample: " + row[0] + " | " + row[1] + " = " +
                                 Truncate(NullSafe(row[2]), 40) : "ok",
                ref failed);

            // ── Object/attribute naming convention ───────────────────────
            passed += Probe(sb, "16. Dotted tag-name pattern (<Object>.<Attribute>)",
                "SELECT COUNT(DISTINCT LEFT(TagName, CHARINDEX('.', TagName) - 1)) AS objects, " +
                "COUNT(*) AS dotted_tags " +
                "FROM Tag WHERE TagName LIKE '%.%' AND CHARINDEX('.', TagName) > 1",
                row => "distinct objects: " + row[0] + ", dotted tags: " + row[1],
                ref failed);

            passed += Probe(sb, "17. Sample objects (top 5 by tag count)",
                "SELECT TOP 5 LEFT(TagName, CHARINDEX('.', TagName) - 1) AS obj, COUNT(*) AS n " +
                "FROM Tag WHERE TagName LIKE '%.%' AND CHARINDEX('.', TagName) > 1 " +
                "GROUP BY LEFT(TagName, CHARINDEX('.', TagName) - 1) " +
                "ORDER BY COUNT(*) DESC",
                row => row[0] + " (" + row[1] + " attrs)",
                ref failed,
                allRows: true);

            sb.AppendLine();
            sb.AppendLine("# Summary: " + passed + " passed, " + failed + " failed (of " +
                          (passed + failed) + " probes).");
            return sb.ToString();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private int Probe(StringBuilder sb, string label, string sql,
            Func<object[], string> render, ref int failed, bool allRows = false)
        {
            try
            {
                var result = _client.Run(sql, allRows ? 50 : 5);
                if (result.Rows.Count == 0)
                {
                    sb.AppendLine(Pad("[OK-EMPTY] ", label) + " (no rows — table/view exists but is empty)");
                    return 1;
                }
                if (allRows)
                {
                    sb.AppendLine(Pad("[OK]       ", label));
                    foreach (var row in result.Rows)
                        sb.AppendLine("           - " + render(row));
                }
                else
                {
                    var detail = render(result.Rows[0]);
                    sb.AppendLine(Pad("[OK]       ", label) +
                                  (string.IsNullOrEmpty(detail) ? "" : "  " + detail));
                }
                return 1;
            }
            catch (SqlException ex)
            {
                sb.AppendLine(Pad("[FAIL]     ", label) + "  " + FirstLine(ex.Message));
                failed++;
                return 0;
            }
            catch (Exception ex)
            {
                sb.AppendLine(Pad("[FAIL]     ", label) + "  (" + ex.GetType().Name + ") " +
                              FirstLine(ex.Message));
                failed++;
                return 0;
            }
        }

        private static string Pad(string prefix, string label)
        {
            // Keep label column at fixed width so the report aligns.
            const int width = 60;
            string row = prefix + label;
            if (row.Length < width) row += new string(' ', width - row.Length);
            return row;
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private static string NullSafe(object v)
        {
            return v == null || v is DBNull ? "NULL" : v.ToString();
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOfAny(new[] { '\r', '\n' });
            return nl >= 0 ? s.Substring(0, nl) : s;
        }
    }
}
