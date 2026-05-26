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
using aaMcpServer.Historian;
using aaMcpServer.Mcp;

namespace aaMcpServer.Galaxy
{
    /// <summary>
    /// Thin wrapper over the AVEVA GRAccess COM API for read-only Galaxy browsing.
    ///
    /// Design notes
    /// ============
    /// GRAccess is a COM library that AVEVA ships with the Application Server / ArchestrA
    /// IDE. To avoid taking a proprietary build-time reference on the GRAccess interop
    /// assemblies, we use **late-binding** via <c>Type.GetTypeFromProgID</c> + <c>dynamic</c>.
    /// The project therefore compiles on any machine; at runtime, GRAccess must be
    /// registered on the host (i.e. AVEVA System Platform IDE installed).
    ///
    /// The exact GRAccess API surface (ProgID strings, method names, enum values) can
    /// vary slightly between releases. The names used below match the long-standing
    /// public surface; if your installation differs, adjust <c>GalaxyProgIds</c> in the
    /// config and/or the method calls in <see cref="EnsureConnected"/> and
    /// <see cref="ListObjects"/>.
    /// </summary>
    public sealed class GalaxyClient : IDisposable
    {
        private readonly ServiceConfig _cfg;
        private readonly object _gate = new object();
        private dynamic _grAccess;
        private dynamic _galaxy;
        private bool _connected;
        private bool _disposed;

        public GalaxyClient(ServiceConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        /// <summary>True if the configuration contains enough to attempt a connection.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_cfg.GalaxyNode) &&
            !string.IsNullOrWhiteSpace(_cfg.GalaxyName);

        /// <summary>
        /// Attempts to bind to the GRAccess COM server and log into the configured Galaxy.
        /// Returns null on success or an error message describing what went wrong.
        /// </summary>
        public string TestConnection()
        {
            try { EnsureConnected(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Public read-only API used by the tools ───────────────────────

        /// <summary>
        /// Lists templates or instances in the Galaxy whose tagname matches the supplied
        /// pattern. Returns columns: TagName, DerivedFromName, ConfigVersion, ContainedName.
        /// </summary>
        /// <param name="templatesOnly">true = templates (tagname starts with '$'); false = instances.</param>
        /// <param name="namePattern">Wildcard pattern (GRAccess style, e.g. "*Pump*"). Empty = all.</param>
        /// <param name="limit">Maximum number of rows to return.</param>
        public QueryResult ListObjects(bool templatesOnly, string namePattern, int limit)
        {
            EnsureConnected();
            var result = new QueryResult();
            result.Columns.AddRange(new[]
            {
                "TagName", "DerivedFromName", "ConfigVersion", "ContainedName"
            });

            // Default filter: $* for templates, anything-but-$ for instances.
            string filter = string.IsNullOrWhiteSpace(namePattern)
                ? (templatesOnly ? "$*" : "*")
                : namePattern.Trim();

            dynamic gObjects;
            try
            {
                // Most GRAccess versions expose QueryObjectsByName(NameFilter) which
                // returns an gObjects collection. We then filter template vs instance
                // by the leading '$' in the tagname.
                gObjects = _galaxy.QueryObjectsByName(filter);
            }
            catch (Exception ex)
            {
                throw new ToolException(
                    "Galaxy.QueryObjectsByName failed: " + ex.Message +
                    " (the call name or signature may differ on your GRAccess version)");
            }

            int total = SafeIntProperty(gObjects, "count");
            int added = 0;
            for (int i = 1; i <= total && added < limit; i++)
            {
                dynamic obj;
                try { obj = gObjects.Item(i); } catch { continue; }
                var tagname = SafeStringProperty(obj, "Tagname");
                if (string.IsNullOrEmpty(tagname)) continue;

                bool isTemplate = tagname.StartsWith("$", StringComparison.Ordinal);
                if (templatesOnly && !isTemplate) continue;
                if (!templatesOnly && isTemplate) continue;

                result.Rows.Add(new object[]
                {
                    tagname,
                    SafeStringProperty(obj, "DerivedFromName"),
                    SafeIntProperty(obj, "ConfigVersion"),
                    SafeStringProperty(obj, "ContainedName"),
                });
                added++;
            }
            if (added < total && total > limit) result.Truncated = true;
            return result;
        }

        /// <summary>
        /// Returns metadata + attribute values for a single object by exact tagname.
        /// Columns: Property, Value (one row per attribute / metadata field).
        /// </summary>
        public QueryResult GetObject(string tagname)
        {
            if (string.IsNullOrWhiteSpace(tagname))
                throw new ToolException("Missing required argument 'tagname'.");

            EnsureConnected();

            dynamic gObjects;
            try { gObjects = _galaxy.QueryObjectsByName(tagname.Trim()); }
            catch (Exception ex)
            {
                throw new ToolException("Galaxy.QueryObjectsByName failed: " + ex.Message);
            }

            int count = SafeIntProperty(gObjects, "count");
            if (count == 0)
                throw new ToolException("Object '" + tagname + "' not found in Galaxy '" + _cfg.GalaxyName + "'.");

            dynamic obj = gObjects.Item(1);
            var result = new QueryResult();
            result.Columns.AddRange(new[] { "Property", "Value" });

            // Top-level metadata fields (well-known across GRAccess versions).
            AddRow(result, "TagName", SafeStringProperty(obj, "Tagname"));
            AddRow(result, "DerivedFromName", SafeStringProperty(obj, "DerivedFromName"));
            AddRow(result, "BasedOnName", SafeStringProperty(obj, "BasedOnName"));
            AddRow(result, "ContainedName", SafeStringProperty(obj, "ContainedName"));
            AddRow(result, "Hierarchical", SafeStringProperty(obj, "HierarchicalName"));
            AddRow(result, "ConfigVersion", SafeIntProperty(obj, "ConfigVersion").ToString(CultureInfo.InvariantCulture));
            AddRow(result, "DeploymentStatus", SafeStringProperty(obj, "DeploymentStatus"));
            AddRow(result, "CheckedOutBy", SafeStringProperty(obj, "CheckedOutByUser"));

            // Attributes collection (best-effort; field names vary across versions).
            try
            {
                dynamic attrs = obj.Attributes;
                int n = SafeIntProperty(attrs, "count");
                for (int i = 1; i <= n; i++)
                {
                    dynamic attr;
                    try { attr = attrs.Item(i); } catch { continue; }
                    var name = SafeStringProperty(attr, "Name");
                    if (string.IsNullOrEmpty(name)) continue;
                    var val = SafeStringProperty(attr, "value");
                    AddRow(result, "attr." + name, val);
                }
            }
            catch { /* attribute enumeration may not be supported on every version */ }

            return result;
        }

        // ── Internals ────────────────────────────────────────────────────

        private void EnsureConnected()
        {
            lock (_gate)
            {
                if (_disposed) throw new ToolException("GalaxyClient has been disposed.");
                if (_connected && _galaxy != null) return;

                if (!IsConfigured)
                    throw new ToolException(
                        "Galaxy connection is not configured. Set Galaxy.Node / Galaxy.Name / " +
                        "Galaxy.User / Galaxy.Password in the .config file.");

                Type t = null;
                string triedList = "";
                foreach (var progId in _cfg.GalaxyProgIds)
                {
                    t = Type.GetTypeFromProgID(progId);
                    triedList += (triedList.Length > 0 ? ", " : "") + "'" + progId + "'";
                    if (t != null) break;
                }
                if (t == null)
                    throw new ToolException(
                        "GRAccess COM library not found. Tried ProgIDs: " + triedList +
                        ". Install AVEVA System Platform IDE on this host, or set Galaxy.ProgIds " +
                        "in the .config file to a ProgID that matches your GRAccess installation.");

                _grAccess = Activator.CreateInstance(t);

                dynamic galaxies;
                try { galaxies = _grAccess.QueryGalaxies(_cfg.GalaxyNode); }
                catch (Exception ex)
                {
                    throw new ToolException(
                        "QueryGalaxies('" + _cfg.GalaxyNode + "') failed: " + ex.Message);
                }

                try { _galaxy = galaxies.Item(_cfg.GalaxyName); }
                catch (Exception ex)
                {
                    throw new ToolException(
                        "Galaxy '" + _cfg.GalaxyName + "' not found on node '" +
                        _cfg.GalaxyNode + "': " + ex.Message);
                }
                if (_galaxy == null)
                    throw new ToolException(
                        "Galaxy '" + _cfg.GalaxyName + "' not found on node '" + _cfg.GalaxyNode + "'.");

                try { _galaxy.Login(_cfg.GalaxyUser ?? "", _cfg.GalaxyPassword ?? ""); }
                catch (Exception ex)
                {
                    throw new ToolException(
                        "Login as '" + _cfg.GalaxyUser + "' to Galaxy '" + _cfg.GalaxyName +
                        "' failed: " + ex.Message);
                }

                _connected = true;
            }
        }

        private static void AddRow(QueryResult r, string property, string value)
        {
            r.Rows.Add(new object[] { property, value ?? "" });
        }

        private static string SafeStringProperty(dynamic com, string name)
        {
            try
            {
                var prop = com.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance,
                    null, com, null);
                return prop == null ? "" : prop.ToString();
            }
            catch { return ""; }
        }

        private static int SafeIntProperty(dynamic com, string name)
        {
            try
            {
                var prop = com.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance,
                    null, com, null);
                if (prop == null) return 0;
                int parsed;
                if (int.TryParse(prop.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    return parsed;
                return 0;
            }
            catch { return 0; }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                try { if (_galaxy != null) _galaxy.Logout(); } catch { }
                _galaxy = null;
                _grAccess = null;
                _connected = false;
            }
        }
    }
}
