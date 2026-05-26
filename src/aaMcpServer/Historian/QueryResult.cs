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

namespace aaMcpServer.Historian
{
    /// <summary>
    /// A simple, serialisation-friendly representation of a result set:
    /// the column names plus a list of rows (each row a list of values aligned
    /// to the columns). Includes a flag indicating whether the result was
    /// truncated by the row cap.
    /// </summary>
    public sealed class QueryResult
    {
        public List<string> Columns { get; } = new List<string>();
        public List<object[]> Rows { get; } = new List<object[]>();
        public bool Truncated { get; set; }

        public int RowCount => Rows.Count;

        /// <summary>
        /// Renders the result as a compact, model-friendly text table. Returns a
        /// helpful message when there are no rows.
        /// </summary>
        public string ToText(string emptyMessage = "No rows returned.")
        {
            if (Rows.Count == 0)
                return emptyMessage;

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" | ", Columns));
            sb.AppendLine(new string('-', Math.Min(120, string.Join(" | ", Columns).Length)));

            foreach (var row in Rows)
            {
                var cells = new string[row.Length];
                for (int i = 0; i < row.Length; i++)
                    cells[i] = Format(row[i]);
                sb.AppendLine(string.Join(" | ", cells));
            }

            sb.AppendLine();
            sb.Append(Truncated
                ? "(" + Rows.Count + " rows shown; result was truncated by the row cap.)"
                : "(" + Rows.Count + " row" + (Rows.Count == 1 ? "" : "s") + ".)");

            return sb.ToString();
        }

        private static string Format(object value)
        {
            if (value == null || value is DBNull) return "NULL";
            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            if (value is double d)
                return d.ToString("0.######", CultureInfo.InvariantCulture);
            if (value is float f)
                return f.ToString("0.######", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
