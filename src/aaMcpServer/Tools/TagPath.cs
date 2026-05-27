// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 27-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;

namespace aaMcpServer.Tools
{
    /// <summary>
    /// Helpers for the AVEVA System Platform tag-naming convention
    ///     &lt;ObjectName&gt;.&lt;AttributePath&gt;
    /// where ObjectName is the part before the FIRST dot and AttributePath is
    /// everything after (which may itself contain dots, e.g.
    /// "Engine.IssuedExternalSetsCnt").
    /// </summary>
    internal static class TagPath
    {
        /// <summary>True if the value looks like a full TagName (contains a dot).</summary>
        public static bool IsFullTagName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            int i = value.IndexOf('.');
            return i > 0 && i < value.Length - 1;
        }

        /// <summary>Splits "Object.Attribute.Path" into ("Object", "Attribute.Path"). Returns false if no dot.</summary>
        public static bool TrySplit(string tagName, out string objectName, out string attributePath)
        {
            objectName = null;
            attributePath = null;
            if (string.IsNullOrWhiteSpace(tagName)) return false;
            int i = tagName.IndexOf('.');
            if (i <= 0 || i >= tagName.Length - 1) return false;
            objectName = tagName.Substring(0, i);
            attributePath = tagName.Substring(i + 1);
            return true;
        }

        /// <summary>Returns the object name (substring before first dot) or the whole input if no dot.</summary>
        public static string ObjectOf(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return "";
            int i = tagName.IndexOf('.');
            return i > 0 ? tagName.Substring(0, i) : tagName;
        }

        /// <summary>Returns the attribute path (after first dot) or empty if no dot.</summary>
        public static string AttributeOf(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return "";
            int i = tagName.IndexOf('.');
            return (i > 0 && i < tagName.Length - 1) ? tagName.Substring(i + 1) : "";
        }
    }
}
