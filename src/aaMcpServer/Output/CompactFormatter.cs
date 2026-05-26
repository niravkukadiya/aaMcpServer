// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using aaMcpServer.Historian;

namespace aaMcpServer.Output
{
    /// <summary>
    /// Produces a token-efficient text rendering of a <see cref="QueryResult"/> for
    /// LLM consumption. Lays out response-level metadata in a header line and emits
    /// only what varies per row.
    ///
    /// Design:
    ///   - Timestamps are emitted as epoch milliseconds (UTC).
    ///   - Quality column is dropped when constant across the result; the constant
    ///     value is recorded in the header (e.g. "q=192").
    ///   - QualityDetail is dropped entirely (it is almost always 0 and the model
    ///     rarely needs it; query the table directly if you do).
    ///   - For uniform-interval results (typical for Cyclic / Avg / Min / Max /
    ///     Interpolated / BestFit / Slope / Integral / Counter / ValueState /
    ///     RoundTrip modes) the per-row timestamp is dropped: the header carries
    ///     "t0=&lt;epoch_ms&gt; dt=&lt;step_ms&gt;" and rows become a bare value stream.
    ///     Recover any row's time as: t_i = t0 + i * dt.
    ///   - Rows are grouped by tag (one block per tag); a multi-tag query emits
    ///     successive blocks separated by a blank line.
    ///
    /// All headers start with "# " so a downstream parser (or human reader) can tell
    /// metadata from data at a glance.
    /// </summary>
    public static class CompactFormatter
    {
        /// <summary>
        /// Compact format for time-series history results (DateTime, TagName, Value,
        /// Quality, QualityDetail columns).
        /// </summary>
        public static string FormatHistory(QueryResult r, string mode, bool timesAreUtc,
            string emptyMessage = "0 rows")
        {
            if (r == null || r.Rows.Count == 0) return emptyMessage;

            int iTime = FindCol(r, "DateTime");
            int iTag  = FindCol(r, "TagName");
            int iVal  = FindCol(r, "Value");
            int iQ    = FindCol(r, "Quality");

            var groups = GroupByTag(r, iTag);

            var sb = new StringBuilder();
            sb.Append("# his_query_history compact, mode=").Append(mode ?? "?");
            sb.AppendLine(", times=epoch_ms");

            bool first = true;
            foreach (var kv in groups)
            {
                if (!first) sb.AppendLine();
                first = false;

                string tag = kv.Key;
                var rows = kv.Value;

                // Compute epoch-ms timestamps once.
                long[] t = new long[rows.Count];
                for (int i = 0; i < rows.Count; i++)
                    t[i] = ToEpochMs(rows[i][iTime], timesAreUtc);

                // Uniform-interval detection.
                bool uniform = rows.Count >= 2;
                long dt = uniform ? (t[1] - t[0]) : 0;
                for (int i = 2; i < rows.Count && uniform; i++)
                {
                    if (t[i] - t[i - 1] != dt) uniform = false;
                }

                // Constant-quality detection.
                bool constantQ = iQ >= 0 && rows.Count > 0;
                string qConst = constantQ ? ToInvariant(rows[0][iQ]) : "";
                for (int i = 1; i < rows.Count && constantQ; i++)
                {
                    if (!string.Equals(ToInvariant(rows[i][iQ]), qConst, StringComparison.Ordinal))
                        constantQ = false;
                }

                // Header line: response-level metadata.
                var hdr = new StringBuilder();
                hdr.Append("# tag=").Append(tag).Append(" n=").Append(rows.Count);
                if (uniform)
                {
                    hdr.Append(" t0=").Append(t[0].ToString(CultureInfo.InvariantCulture));
                    hdr.Append(" dt=").Append(dt.ToString(CultureInfo.InvariantCulture));
                }
                if (constantQ) hdr.Append(" q=").Append(qConst);
                sb.AppendLine(hdr.ToString());

                // Column legend: what each space-separated row column means.
                var cols = new List<string>(3);
                if (!uniform) cols.Add("t");
                cols.Add("v");
                if (!constantQ && iQ >= 0) cols.Add("q");
                sb.Append("# columns: ").AppendLine(string.Join(",", cols));

                // Rows.
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    bool wroteAnything = false;
                    if (!uniform)
                    {
                        sb.Append(t[i].ToString(CultureInfo.InvariantCulture));
                        wroteAnything = true;
                    }
                    if (wroteAnything) sb.Append(' ');
                    sb.Append(FormatValue(iVal >= 0 ? row[iVal] : null));
                    if (!constantQ && iQ >= 0)
                    {
                        sb.Append(' ').Append(ToInvariant(row[iQ]));
                    }
                    sb.AppendLine();
                }
            }

            if (r.Truncated) sb.AppendLine("# truncated by row cap");
            return sb.ToString();
        }

        /// <summary>
        /// Compact format for live values (TagName, DateTime, Value, Quality, QualityDetail).
        /// One row per tag.
        /// </summary>
        public static string FormatLive(QueryResult r, bool timesAreUtc,
            string emptyMessage = "0 rows")
        {
            if (r == null || r.Rows.Count == 0) return emptyMessage;

            int iTag  = FindCol(r, "TagName");
            int iTime = FindCol(r, "DateTime");
            int iVal  = FindCol(r, "Value");
            int iQ    = FindCol(r, "Quality");

            // Constant-quality detection (often all-good).
            bool constantQ = iQ >= 0 && r.Rows.Count > 0;
            string qConst = constantQ ? ToInvariant(r.Rows[0][iQ]) : "";
            for (int i = 1; i < r.Rows.Count && constantQ; i++)
                if (!string.Equals(ToInvariant(r.Rows[i][iQ]), qConst, StringComparison.Ordinal))
                    constantQ = false;

            var sb = new StringBuilder();
            sb.Append("# his_get_live_values compact, times=epoch_ms, n=").Append(r.Rows.Count);
            if (constantQ) sb.Append(" q=").Append(qConst);
            sb.AppendLine();

            var cols = new List<string> { "n", "t", "v" };
            if (!constantQ && iQ >= 0) cols.Add("q");
            sb.Append("# columns: ").AppendLine(string.Join(",", cols));

            foreach (var row in r.Rows)
            {
                sb.Append(iTag >= 0 ? Convert.ToString(row[iTag]) : "");
                sb.Append(' ').Append(ToEpochMs(iTime >= 0 ? row[iTime] : null, timesAreUtc)
                    .ToString(CultureInfo.InvariantCulture));
                sb.Append(' ').Append(FormatValue(iVal >= 0 ? row[iVal] : null));
                if (!constantQ && iQ >= 0) sb.Append(' ').Append(ToInvariant(row[iQ]));
                sb.AppendLine();
            }
            if (r.Truncated) sb.AppendLine("# truncated by row cap");
            return sb.ToString();
        }

        /// <summary>
        /// Generic compact CSV for tabular results that don't have time-series structure
        /// (search_tags, query_alarms_events). Any DateTime cells are emitted as epoch ms;
        /// numeric / string cells pass through. Column order is preserved.
        /// </summary>
        public static string FormatTable(QueryResult r, string toolName, bool timesAreUtc,
            string emptyMessage = "0 rows")
        {
            if (r == null || r.Rows.Count == 0) return emptyMessage;

            var sb = new StringBuilder();
            sb.Append("# ").Append(toolName).Append(" compact, n=").Append(r.Rows.Count);
            sb.AppendLine(", times=epoch_ms");
            sb.Append("# columns: ").AppendLine(string.Join(",", r.Columns));

            foreach (var row in r.Rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(FormatCell(row[i], timesAreUtc));
                }
                sb.AppendLine();
            }
            if (r.Truncated) sb.AppendLine("# truncated by row cap");
            return sb.ToString();
        }

        // ── helpers ─────────────────────────────────────────────────────

        private static int FindCol(QueryResult r, string name)
        {
            for (int i = 0; i < r.Columns.Count; i++)
                if (string.Equals(r.Columns[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static Dictionary<string, List<object[]>> GroupByTag(QueryResult r, int iTag)
        {
            // Use a dictionary that preserves insertion order across .NET versions.
            var groups = new Dictionary<string, List<object[]>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var row in r.Rows)
            {
                string tag = iTag >= 0 ? Convert.ToString(row[iTag]) ?? "" : "";
                if (!groups.TryGetValue(tag, out var list))
                {
                    groups[tag] = list = new List<object[]>();
                    order.Add(tag);
                }
                list.Add(row);
            }
            // Re-emit in insertion order (Dictionary<TKey,TValue> in .NET Framework
            // happens to preserve insertion order in practice but the spec doesn't
            // guarantee it; build a fresh ordered dictionary just in case).
            var ordered = new Dictionary<string, List<object[]>>(groups.Count);
            foreach (var k in order) ordered[k] = groups[k];
            return ordered;
        }

        private static long ToEpochMs(object value, bool timesAreUtc)
        {
            if (value == null || value is DBNull) return 0L;
            DateTime dt;
            if (value is DateTime d) dt = d;
            else if (!DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                         DateTimeStyles.None, out dt)) return 0L;

            // Treat Unspecified-kind values according to the Historian.TimesAreUtc switch.
            DateTime utc;
            if (dt.Kind == DateTimeKind.Utc) utc = dt;
            else if (dt.Kind == DateTimeKind.Local) utc = dt.ToUniversalTime();
            else utc = timesAreUtc
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();

            var ts = new DateTimeOffset(utc, TimeSpan.Zero);
            return ts.ToUnixTimeMilliseconds();
        }

        private static string ToInvariant(object value)
        {
            if (value == null || value is DBNull) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Renders a numeric/string value without padding. Preserves precision.</summary>
        private static string FormatValue(object v)
        {
            if (v == null || v is DBNull) return "null";
            if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (v is float f)  return f.ToString("R", CultureInfo.InvariantCulture);
            if (v is decimal m) return m.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        /// <summary>Renders a single cell for FormatTable, escaping commas/newlines in strings.</summary>
        private static string FormatCell(object v, bool timesAreUtc)
        {
            if (v == null || v is DBNull) return "";
            if (v is DateTime || v is DateTimeOffset)
                return ToEpochMs(v, timesAreUtc).ToString(CultureInfo.InvariantCulture);
            if (v is double || v is float || v is decimal) return FormatValue(v);

            var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            // Minimal CSV escaping: quote if the cell contains a comma, quote, or newline.
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
