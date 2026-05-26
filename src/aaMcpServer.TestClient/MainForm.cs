// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.TestClient
{
    /// <summary>
    /// A simple WinForms harness for exercising the aa Mcp Server while
    /// it runs in console mode (aaMcpServer.exe --console). Connect performs the
    /// MCP handshake and lists the tools; pick a tool, edit the JSON arguments and call.
    ///
    /// UI layout lives in MainForm.Designer.cs; all behaviour lives here.
    /// </summary>
    public partial class MainForm : Form
    {
        private McpHttpClient _client;
        private readonly Dictionary<string, JObject> _toolsByName =
            new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            InitializeComponent();
        }

        // ── Event handlers (wired in the designer) ───────────────────────

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            await ConnectAsync();
        }

        private async void btnCall_Click(object sender, EventArgs e)
        {
            await CallToolAsync();
        }

        private void cmbTools_SelectedIndexChanged(object sender, EventArgs e)
        {
            OnToolSelected();
        }

        // ── MCP flow ──────────────────────────────────────────────────────

        private async Task ConnectAsync()
        {
            btnConnect.Enabled = false;
            SetStatus("Connecting...", Color.DimGray);
            _toolsByName.Clear();
            cmbTools.Items.Clear();
            btnCall.Enabled = false;

            try
            {
                _client?.Dispose();
                _client = new McpHttpClient(txtEndpoint.Text.Trim());

                // 1) initialize
                var init = await _client.InitializeAsync();
                var result = init?["result"] as JObject;
                if (result == null)
                {
                    ShowResponse(init);
                    SetStatus("initialize failed - see response.", Color.Firebrick);
                    return;
                }
                var proto = (string)result["protocolVersion"];
                var info = result["serverInfo"] as JObject;
                var serverName = info != null ? (string)info["name"] : "?";

                // 2) notifications/initialized
                await _client.SendInitializedAsync();

                // 3) tools/list
                var listResp = await _client.ListToolsAsync();
                var tools = (listResp?["result"] as JObject)?["tools"] as JArray;
                if (tools == null)
                {
                    ShowResponse(listResp);
                    SetStatus("tools/list failed - see response.", Color.Firebrick);
                    return;
                }

                foreach (var t in tools)
                {
                    var to = t as JObject;
                    var name = (string)to?["name"];
                    if (string.IsNullOrEmpty(name)) continue;
                    _toolsByName[name] = to;
                    cmbTools.Items.Add(name);
                }

                if (cmbTools.Items.Count > 0)
                {
                    cmbTools.SelectedIndex = 0;
                    btnCall.Enabled = true;
                }

                SetStatus(
                    "Connected to '" + serverName + "' (protocol " + proto + "). " +
                    cmbTools.Items.Count + " tool(s) available.",
                    Color.SeaGreen);
                ShowResponse(listResp, "tools/list");
            }
            catch (Exception ex)
            {
                SetStatus("Connection error: " + ex.Message, Color.Firebrick);
                txtResult.Text = ex.ToString();
            }
            finally
            {
                btnConnect.Enabled = true;
            }
        }

        private void OnToolSelected()
        {
            var name = cmbTools.SelectedItem as string;
            JObject tool;
            if (name == null || !_toolsByName.TryGetValue(name, out tool)) return;

            txtDescription.Text = (string)tool["description"] ?? "";
            var schema = tool["inputSchema"] as JObject;
            txtArgs.Text = BuildTemplate(schema).ToString(Formatting.Indented);
        }

        private async Task CallToolAsync()
        {
            var name = cmbTools.SelectedItem as string;
            if (name == null || _client == null) return;

            JObject args;
            try
            {
                var text = txtArgs.Text.Trim();
                args = string.IsNullOrEmpty(text) ? new JObject() : JObject.Parse(text);
            }
            catch (JsonException jex)
            {
                SetStatus("Arguments are not valid JSON: " + jex.Message, Color.Firebrick);
                return;
            }

            btnCall.Enabled = false;
            SetStatus("Calling " + name + "...", Color.DimGray);
            try
            {
                var resp = await _client.CallToolAsync(name, args);
                ShowResponse(resp, "tools/call " + name);

                var isError = (resp?["result"] as JObject)?["isError"]?.ToObject<bool>() ?? false;
                SetStatus(
                    isError ? name + " returned an error (see response)." : name + " succeeded.",
                    isError ? Color.DarkOrange : Color.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus("Call error: " + ex.Message, Color.Firebrick);
                txtResult.Text = ex.ToString();
            }
            finally
            {
                btnCall.Enabled = true;
            }
        }

        // ── Rendering / helpers ───────────────────────────────────────────

        /// <summary>Renders the request that was sent, the raw response, and any text content.</summary>
        private void ShowResponse(JObject response, string label = null)
        {
            var sb = new StringBuilder();
            if (_client?.LastRequestJson != null)
            {
                sb.AppendLine("// ---- REQUEST" + (label != null ? " (" + label + ")" : "") + " ----");
                sb.AppendLine(_client.LastRequestJson);
                sb.AppendLine();
            }

            sb.AppendLine("// ---- RESPONSE ----");
            sb.AppendLine(response != null
                ? response.ToString(Formatting.Indented)
                : "(no body / 202 acknowledged)");

            var content = (response?["result"] as JObject)?["content"] as JArray;
            if (content != null)
            {
                sb.AppendLine();
                sb.AppendLine("// ---- TEXT CONTENT ----");
                foreach (var c in content)
                {
                    if ((string)c?["type"] == "text")
                        sb.AppendLine((string)c["text"]);
                }
            }

            txtResult.Text = sb.ToString();
            txtResult.SelectionStart = 0;
            txtResult.ScrollToCaret();
        }

        /// <summary>Builds a skeleton arguments object from a tool's JSON input schema.</summary>
        private static JObject BuildTemplate(JObject schema)
        {
            var o = new JObject();
            var props = schema?["properties"] as JObject;
            if (props == null) return o;

            foreach (var p in props)
            {
                var spec = p.Value as JObject;
                var type = (string)spec?["type"];
                o[p.Key] = DefaultForType(type, spec);
            }
            return o;
        }

        private static JToken DefaultForType(string type, JObject spec)
        {
            switch (type)
            {
                case "array":
                    var itemType = (string)(spec?["items"] as JObject)?["type"];
                    return new JArray { itemType == "string" ? (JToken)"TAGNAME" : 0 };
                case "integer":
                case "number":
                    return 0;
                case "boolean":
                    return false;
                case "object":
                    return new JObject();
                default:
                    return "";
            }
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text = text;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Set the splitter once the control has a real height (kept out of the
            // designer so InitializeComponent can never throw on an invalid distance).
            try
            {
                var third = Math.Max(splitMain.Panel1MinSize + 10, splitMain.Height / 3);
                if (third < splitMain.Height - splitMain.Panel2MinSize)
                    splitMain.SplitterDistance = third;
            }
            catch { /* ignore sizing edge cases */ }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _client?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
