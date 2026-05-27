# aa Mcp Server (for Microsoft Copilot)

A Model Context Protocol (MCP) server, written in **C# on .NET Framework 4.8**, that
exposes the **AVEVA (Wonderware) Historian** to Microsoft Copilot. It speaks the MCP
**Streamable HTTP** transport that Copilot Studio expects, queries the Historian
directly over SQL, and ships as a single self-contained Windows Service.

This v1.2 release is a **lean demo + extensibility template**: the server registers
just one tool out of the box (`his_search_tags`) and includes a comprehensive
[`docs/CreateNewTool.md`](docs/CreateNewTool.md) walkthrough for adding more.

## Out-of-the-box tool

| Tool | Purpose |
| --- | --- |
| `his_search_tags` | Find Historian tags by name/description, optionally filtered by type (analog/discrete/string). |

To add more tools (live values, history retrieval, alarms, Galaxy/GRAccess, anything
else): see **[docs/CreateNewTool.md](docs/CreateNewTool.md)**.

## Architecture

```
Microsoft Copilot Studio
        │  (HTTP, JSON-RPC 2.0 over Streamable HTTP, POST /mcp)
        ▼
StreamableHttpServer  (System.Net.HttpListener)
        ▼
McpServer             (initialize / tools/list / tools/call / ping)
        ▼
ToolRegistry          (your ITool implementations live here)
        ▼
HistorianClient (System.Data.SqlClient) ──► AVEVA Historian "Runtime" DB
```

Source layout (`src/aaMcpServer/`):

- `Program.cs` — console (`--console`) vs Windows Service; `--install` / `--uninstall`.
- `AppHost.cs` — wires config → data layer → tools → MCP server → HTTP transport.
- `HistorianMcpService.cs` — `ServiceBase` wrapper for Windows Service hosting.
- `Http/StreamableHttpServer.cs` — Streamable HTTP transport on `HttpListener`.
- `Mcp/` — JSON-RPC envelopes, MCP request dispatcher, tool registry/interface.
- `Historian/` — `HistorianClient` (SQL access), `SqlSanitize`, `QueryResult`.
- `Output/CompactFormatter.cs` — token-efficient output (epoch_ms, columnar dedup).
- `Logging/RollingFileLogger.cs` — rolling 6-hour log file with auto-zip + retention.
- `Tools/` — tool implementations.
- `Properties/AssemblyInfo.cs` — assembly metadata.
- `App.config` — all runtime settings.

The solution also contains `src/aaMcpServer.TestClient/` — a WinForms GUI to
exercise the server locally without Copilot.

## Project format

Both projects use the **classic (non-SDK) .csproj XML format** with `PackageReference`
for NuGet dependencies. This works in Visual Studio 2017+ and on `dotnet build` /
`msbuild` across platforms. A `Properties/AssemblyInfo.cs` carries assembly metadata
(Title, Product, Version, Guid).

## Prerequisites

- Windows with **.NET Framework 4.8**.
- **Visual Studio 2017+** (or the .NET SDK / MSBuild) to build.
- Network access from the server host to the AVEVA Historian **SQL Server**.
- A least-privilege, read-only **SQL Server login** for the Historian `Runtime` DB.

## Build

In Visual Studio: open `aaMcpServer.sln` → Build.

From the command line:

```powershell
dotnet restore aaMcpServer.sln
dotnet build   aaMcpServer.sln -c Release
```

Output: `src\aaMcpServer\bin\Release\aaMcpServer.exe` (with
`Newtonsoft.Json.dll` and `aaMcpServer.exe.config` alongside it).

## Configuration

Edit `aaMcpServer.exe.config` (built from `App.config`):

| Key | Meaning |
| --- | --- |
| `Http.Prefix` | Listener URI prefix. `http://+:8080/` = all interfaces (needs URL ACL); `http://localhost:8080/` = local only. |
| `Http.McpPath` | MCP endpoint path. Copilot connects to `http://<host>:8080/mcp`. |
| `Historian.Server` | SQL Server instance hosting the Historian. |
| `Historian.Database` | Runtime database name (default `Runtime`). |
| `Historian.User` / `Historian.Password` | SQL Server authentication credentials. |
| `Historian.CommandTimeoutSeconds` | Per-query timeout. |
| `Historian.ExtraConnectionOptions` | Appended verbatim to the connection string. |
| `Historian.TimesAreUtc` | `true` if the Historian returns UTC timestamps. Default `false` (treat as local). |
| `Limits.MaxRows` | Hard cap on rows returned by any tool call. |
| `Output.Format` | `compact` (token-efficient) or `table` (verbose pipe-table). Default `compact`. Overridable per call via the `format` argument. |
| `Log.Directory` | Root folder for log files. |
| `Log.FilePrefix` | Filename root for log files. |
| `Log.RotationHours` | Hours per log file (must divide 24; default `6` = 4 files/day). |
| `Log.RetentionDays` | Delete date folders older than this. |
| `Log.MinLevel` | Minimum level recorded: `TRACE`, `DEBUG`, `INFO`, `WARN`, `ERROR`. |

## Logging

The server writes detailed, level-tagged lines to a **rolling log file** (and, when
interactive, echoes them to the console):

- Rotates every `Log.RotationHours` (default 6, i.e. 4 files/day) into date
  sub-folders: `<Log.Directory>/<yyyy-MM-dd>/<prefix>-HH-00-HH-00.txt`.
- Auto-zips finished slot files on rotation (and on shutdown).
- Deletes date folders older than `Log.RetentionDays` (default 7).
- Never throws from the write path; tracks health counters.

## Output format (`compact` vs `table`)

Default `compact` rendering is heavily optimised for LLM consumption:

- Epoch-millisecond timestamps (13 chars) instead of ISO strings (23 chars).
- A header line carries response-level metadata (tag, mode, t0+dt for uniform
  intervals, constant quality, count); rows only carry what varies.
- ~70 % token reduction on typical Cyclic 1000-point history queries.

Pass `format: "table"` on any tool call (or set `Output.Format = table` in
`App.config`) to switch to the verbose pipe-separated table — useful for eyeballing
results in the test client.

## Running

### Console (development)

```powershell
aaMcpServer.exe --console
```

Quick smoke test:
```powershell
curl -Method POST http://localhost:8080/mcp `
  -ContentType "application/json" `
  -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

#### WinForms test client

`src\aaMcpServer.TestClient\bin\Release\aaMcpServer.TestClient.exe` (or run from
Visual Studio) talks to the server exactly the way Copilot does. Click
**Connect / List Tools**, pick a tool, edit the auto-generated JSON arguments,
click **Call Tool** — the response pane shows the raw JSON-RPC request, the
response, and the extracted text content.

### As a Windows Service

Service name is **`aaMcpServer`**. From an **elevated** prompt:

```powershell
aaMcpServer.exe --install
netsh http add urlacl url=http://+:8080/ user="NT AUTHORITY\NETWORK SERVICE"
netsh advfirewall firewall add rule name="aaMcpServer" dir=in action=allow protocol=TCP localport=8080
net start aaMcpServer
```

To remove:
```powershell
aaMcpServer.exe --uninstall
```

## Connecting from Microsoft Copilot Studio

Copilot Studio reaches an MCP server through a **custom connector** marked with
`x-ms-agentic-protocol: mcp-streamable-1.0`. Minimal OpenAPI (Swagger 2.0):

```yaml
swagger: '2.0'
info:
  title: aa Mcp Server
  description: Query the AVEVA Historian via MCP.
  version: '1.0.0'
host: YOUR_SERVER_HOST:8080
basePath: /
schemes:
  - http
paths:
  /mcp:
    post:
      summary: aa Mcp Server endpoint
      operationId: InvokeMCP
      x-ms-agentic-protocol: mcp-streamable-1.0
      responses:
        '200':
          description: Success
```

In Copilot Studio, add a tool of type **Model Context Protocol**, point at
`http://<host>:8080/mcp`, choose **No authentication**, and the registered tools
appear on the agent.

## Extending the server

See **[docs/CreateNewTool.md](docs/CreateNewTool.md)** for a step-by-step guide on
adding new MCP tools. Topics covered: the `ITool` interface, naming conventions
(`his_` / `gr_` prefixes), JSON Schema for arguments, the helper APIs
(`Args`, `SqlSanitize`, `HistorianClient`, `QueryResult`, `CompactFormatter`),
registering tools in `AppHost`, listing files in the csproj, a fully worked
example (`his_get_live_values`), testing via the WinForms client, and common
pitfalls.

## Security notes

- **No endpoint auth by design.** Run on a trusted/internal network or terminate
  TLS + API-key/OAuth at a reverse proxy in front of the listener.
- **Least-privilege SQL login** for the Historian tools (read-only on `Runtime`).
- **Injection-safe SQL** — all tag names, timestamps, retrieval modes and configured
  identifiers are validated/escaped before being placed in SQL (see `SqlSanitize`).

## Reference

Historian behaviour follows the *AVEVA Historian Retrieval Guide* — the
`History`/`Live`/`Tag` tables and the `wwRetrievalMode`, `wwCycleCount`,
`wwResolution` time-domain extensions.

## License

Licensed under the **Apache License, Version 2.0**. See the [`LICENSE`](LICENSE)
file for the full text and [`NOTICE`](NOTICE) for attributions. Each source file
carries an SPDX header (`SPDX-License-Identifier: Apache-2.0`).

## Trademarks

"AVEVA", "Wonderware", "ArchestrA" and "GRAccess" are trademarks of AVEVA Group
plc and/or its subsidiaries. This project is an independent connector and is
**not affiliated with, sponsored by, or endorsed by AVEVA**. Those names are
used only to describe the systems this software interoperates with. No AVEVA
software, documentation, or other proprietary materials are included in or
distributed with this project.
