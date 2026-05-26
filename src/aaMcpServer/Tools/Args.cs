// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using aaMcpServer.Mcp;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    /// <summary>Convenience accessors for reading values out of a tools/call arguments object.</summary>
    internal static class Args
    {
        public static string GetString(JObject args, string name, string fallback = null)
        {
            var tok = args?[name];
            if (tok == null || tok.Type == JTokenType.Null) return fallback;
            return tok.Type == JTokenType.String ? (string)tok : tok.ToString();
        }

        public static string GetRequiredString(JObject args, string name)
        {
            var v = GetString(args, name);
            if (string.IsNullOrWhiteSpace(v))
                throw new ToolException("Missing required argument '" + name + "'.");
            return v;
        }

        public static int GetInt(JObject args, string name, int fallback)
        {
            var tok = args?[name];
            if (tok == null || tok.Type == JTokenType.Null) return fallback;
            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
                return (int)tok;
            int parsed;
            return int.TryParse(tok.ToString(), out parsed) ? parsed : fallback;
        }

        public static bool HasValue(JObject args, string name)
        {
            var tok = args?[name];
            return tok != null && tok.Type != JTokenType.Null;
        }

        /// <summary>
        /// Reads a string array argument. Accepts either a JSON array of strings or a
        /// single comma-separated string (which some model clients send).
        /// </summary>
        public static List<string> GetStringArray(JObject args, string name)
        {
            var result = new List<string>();
            var tok = args?[name];
            if (tok == null || tok.Type == JTokenType.Null) return result;

            if (tok.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)tok)
                {
                    var s = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s.Trim());
                }
            }
            else
            {
                var raw = tok.ToString();
                foreach (var part in raw.Split(','))
                    if (!string.IsNullOrWhiteSpace(part)) result.Add(part.Trim());
            }
            return result;
        }

        public static List<string> GetRequiredStringArray(JObject args, string name)
        {
            var list = GetStringArray(args, name);
            if (list.Count == 0)
                throw new ToolException("Missing required argument '" + name +
                    "' (expected a non-empty list).");
            return list;
        }
    }
}
