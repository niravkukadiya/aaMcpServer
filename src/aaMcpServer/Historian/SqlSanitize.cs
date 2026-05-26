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
using System.Text.RegularExpressions;
using aaMcpServer.Mcp;

namespace aaMcpServer.Historian
{
    /// <summary>
    /// Helpers that turn untrusted tool arguments into safe SQL fragments.
    ///
    /// The AVEVA Historian OLE DB provider tables (History, Live, ...) are queried
    /// through literal SQL because the provider's parsing of the ww* time-domain
    /// extensions does not reliably accept SqlParameters. To stay safe we validate
    /// and escape every value before it is embedded in a statement.
    /// </summary>
    public static class SqlSanitize
    {
        // Canonical wwRetrievalMode tokens accepted by the Historian. Matched
        // case-insensitively; the canonical spelling is emitted in the SQL.
        private static readonly Dictionary<string, string> RetrievalModes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cyclic", "Cyclic" },
                { "Delta", "Delta" },
                { "Full", "Full" },
                { "Interpolated", "Interpolated" },
                { "BestFit", "BestFit" },
                { "Average", "Average" },
                { "Avg", "Average" },
                { "Min", "Min" },
                { "Minimum", "Min" },
                { "Max", "Max" },
                { "Maximum", "Max" },
                { "Integral", "Integral" },
                { "Slope", "Slope" },
                { "Counter", "Counter" },
                { "ValueState", "ValueState" },
                { "RoundTrip", "RoundTrip" },
            };

        private static readonly Regex IdentifierPattern =
            new Regex(@"^[A-Za-z0-9_\.\[\]]+$", RegexOptions.Compiled);

        /// <summary>Escapes a string for use inside a single-quoted T-SQL literal.</summary>
        public static string EscapeLiteral(string value)
        {
            if (value == null) return string.Empty;
            // Double single quotes; strip control characters that have no business
            // in a tag name / literal.
            var cleaned = Regex.Replace(value, @"[\x00-\x1F]", string.Empty);
            return cleaned.Replace("'", "''");
        }

        /// <summary>Wraps a value as a quoted, escaped T-SQL string literal.</summary>
        public static string QuoteLiteral(string value)
        {
            return "'" + EscapeLiteral(value) + "'";
        }

        /// <summary>Validates and canonicalises a retrieval mode, defaulting to Cyclic.</summary>
        public static string RetrievalMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "Cyclic";
            string canonical;
            if (RetrievalModes.TryGetValue(mode.Trim(), out canonical))
                return canonical;
            throw new ToolException(
                "Unsupported retrievalMode '" + mode + "'. Valid values: Cyclic, Delta, Full, " +
                "Interpolated, BestFit, Average, Min, Max, Integral, Slope, Counter, " +
                "ValueState, RoundTrip.");
        }

        /// <summary>
        /// Parses a date/time string (ISO-8601 or common formats) and returns it in the
        /// 'yyyy-MM-dd HH:mm:ss.fff' format the Historian expects, as a quoted literal.
        /// </summary>
        public static string QuoteDateTime(string value, string argName)
        {
            DateTime dt;
            if (!TryParseDateTime(value, out dt))
                throw new ToolException("Invalid " + argName + " value '" + value +
                    "'. Use a date/time such as '2026-05-25 14:30:00'.");
            return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";
        }

        public static bool TryParseDateTime(string value, out DateTime dt)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeLocal, out dt)
                   || DateTime.TryParse(value, CultureInfo.CurrentCulture,
                       DateTimeStyles.AssumeLocal, out dt);
        }

        /// <summary>Clamps an integer argument into a valid range.</summary>
        public static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Validates a SQL identifier coming from configuration (table/view/column
        /// names). Throws if it contains anything other than letters, digits,
        /// underscore, dot or square brackets.
        /// </summary>
        public static string Identifier(string value, string what)
        {
            if (string.IsNullOrWhiteSpace(value) || !IdentifierPattern.IsMatch(value))
                throw new ToolException("Invalid configured " + what + ": '" + value + "'.");
            return value;
        }

        /// <summary>
        /// Builds a quoted, comma-separated IN(...) list from tag names. Throws if empty.
        /// </summary>
        public static string TagNameInList(IEnumerable<string> tagNames)
        {
            var parts = new List<string>();
            foreach (var t in tagNames)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                parts.Add(QuoteLiteral(t.Trim()));
            }
            if (parts.Count == 0)
                throw new ToolException("At least one tag name is required.");
            return string.Join(", ", parts);
        }
    }
}
