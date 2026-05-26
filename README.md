# aa Mcp Server (for Microsoft Copilot)

A Model Context Protocol (MCP) server, written in **C# on .NET Framework 4.8**, that
exposes the **AVEVA (Wonderware) Historian** (via direct SQL) and, optionally, an
**ArchestrA Galaxy** (via the GRAccess COM library) to Microsoft Copilot. It speaks the
MCP **Streamable HTTP** transport that Copilot Studio expects.

## What it does

The server advertises the following tools to Copilot. The Historian tools are always
active; the Galaxy tools are only registered when `Galaxy.Enabled = true` and a Galaxy
node + name are configured.

| Tool | Purpose |
| --- | --- |
| `his_search_tags` | Find Historian tags by name/description, optionally filtered by type (analog/discrete/string). |
| `his_get_live_values` | Read the latest current value + quality for one or more tags (the `Live` table). |
| `his_query_history` | Retrieve time-series data over a range from the `History` table, with retrieval modes (Cyclic, Delta, Full, Interpolated, BestFit, Average, Min, Max, Integral, Slope, Counter, ValueState, RoundTrip) and the `wwCycleCount` / `wwResolution` time-domain extensions. |
| `his_query_alarms_events` | Query alarm/event history over a range from a configurable alarm view/table. |
| `gr_list_objects` *(opt.)* | List ArchestrA Galaxy templates or instances by name pattern (via GRAccess). |
| `gr_get_object` *(opt.)* | Return metadata + attribute values for a single Galaxy object by tagname. |

## Architecture

```
Microsoft Copilot Studio
        │  (HTTPS/HTTP, JSON-RPC 2.0 over Streamable HTTP, POST /mcp)
        ▼
StreamableHttpServer  (System.Net.HttpListener)
        ▼
McpServer             (initialize / tools/list / tools/call / ping)
        ▼
Tools                 (his_search_tags, his_get_live_values, his_query_history,
                       his_query_alarms_events, gr_list_objects,
                       gr_get_object)
        ▼
HistorianClient (System.Data.SqlClient) ──► AVEVA Historian "Runtime" DB
GalaxyClient    (GRAccess via late-bound COM) ──► ArchestrA Galaxy
```

Source layout (`src/aaMcpServer/`):

- `Program.cs` — entry point; console (`--console`) vs Windows Service; `--install` / `--uninstall`.
- `AppHost.cs` — wires config → data layers → tools → MCP server → HTTP transport.
- `HistorianMcpService.cs` — `ServiceBase` wrapper for running as a Windows Service.
- `Http/StreamableHttpServer.cs` — the Streamable HTTP transport on `HttpListener`.
- `Mcp/` — JSON-RPC envelopes, the MCP request dispatcher, the tool registry/interface.
- `Historian/` — `HistorianClient` (SQL access), `SqlSanitize`, `QueryResult`.
- `Galaxy/` — `GalaxyClient` (late-bound GRAccess COM wrapper).
- `Logging/RollingFileLogger.cs` — rolling 6-hour log file with auto-zip + retention.
- `Tools/` — the tool implementations (Historian + Galaxy).
- `App.config` — all runtime settings.

The solution also contains `src/aaMcpServer.TestClient/` — a WinForms GUI for
testing the server locally (see *Running → WinForms test client*).

## Prerequisites

- Windows with **.NET Framework 4.8**.
- **Visual Studio 2022** (or the .NET SDK / MSBuild) to build.
- For Historian tools: network access to the AVEVA Historian **SQL Server** and a
  least-privilege read-only SQL Server login.
- For Galaxy tools: **AVEVA System Platform IDE** installed on the host (registers the
  GRAccess COM library) and a Galaxy user with permission to read the Galaxy.

## Build

```powershell
dotnet build aaMcpServer.sln -c Release
```

Output: `src\aaMcpServer\bin\Release\net48\aaMcpServer.exe` (with
`Newtonsoft.Json.dll` and `aaMcpServer.exe.config` alongside it). The
`Microsoft.NETFramework.ReferenceAssemblies` package is a build-time-only dependency
so the project compiles on CI without Windows; it has no runtime effect.

## Configuration

Edit `aaMcpServer.exe.config` (produced from `App.config`) next to the binary:

| Key | Meaning |
| --- | --- |
| `Http.Prefix` | URI prefix the listener binds to. `http://+:8080/` = all interfaces (needs URL ACL); `http://localhost:8080/` = local only. |
| `Http.McpPath` | Path of the MCP endpoint. Copilot connects to `http://<host>:8080/mcp`. |
| `Historian.Server` | SQL Server instance hosting the Historian. |
| `Historian.Database` | Runtime database name (default `Runtime`). |
| `Historian.User` / `Historian.Password` | SQL Server authentication credentials. |
| `Historian.CommandTimeoutSeconds` | Per-query timeout. |
| `Historian.ExtraConnectionOptions` | Appended verbatim to the connection string. |
| `Historian.TimesAreUtc` | `true` if the Historian returns UTC timestamps. Default `false` (treat unspecified-kind times as local). Used when converting to epoch ms for compact output. |
| `Output.Format` | Default output for HIS tools: `compact` (token-efficient: epoch_ms, columnar header, dedup constants; ~70 % smaller on Cyclic queries) or `table` (verbose pipe-table). Default `compact`. Can be overridden per call via the `format` argument. |
| `Alarms.Source` | View/table for alarm history (e.g. `v_AlarmEventHistory`). |
| `Alarms.TimeColumn` | Timestamp column in that source (e.g. `EventStamp`). |
| `Limits.MaxRows` | Hard cap on rows returned by any tool call. |
| `Log.Directory` | Root folder for log files. |
| `Log.FilePrefix` | Filename root for log files. |
| `Log.RotationHours` | Hours per log file (must divide 24; default `6`). |
| `Log.RetentionDays` | Delete date folders older than this many days. |
| `Log.MinLevel` | Minimum level recorded: `TRACE`, `DEBUG`, `INFO`, `WARN`, `ERROR`. |
| `Galaxy.Enabled` | `true` to register `galaxy_*` tools. Default `false`. |
| `Galaxy.Node` | Galaxy Repository node name. |
| `Galaxy.Name` | Galaxy name on that node. |
| `Galaxy.User` / `Galaxy.Password` | Galaxy credentials (DefaultUser with empty password works for OS-trusted galaxies). |
| `Galaxy.ProgIds` | Comma-separated GRAccess COM ProgIDs to try, in order. |

## Logging

The server writes detailed, level-tagged lines to a **rolling log file** (and, when
interactive, echoes them to the console). The rolling logger
(`Logging/RollingFileLogger.cs`):

- **Rotates** every `Log.RotationHours` hours (default `6`, i.e. 4 files per day),
  organising files into date sub-folders:
  `<Log.Directory>/<yyyy-MM-dd>/<prefix>-HH-00-HH-00.txt`.
- **Auto-zips** finished slot files on rotation (and on shutdown) on a background thread.
- **Cleans up** date folders older than `Log.RetentionDays` (default 7).
- **Never throws** from the write path; tracks health counters.

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

The solution includes `aaMcpServer.TestClient`, a small GUI that performs the full MCP
handshake (`initialize` → `notifications/initialized` → `tools/list`), populates the
tool dropdown, auto-generates a JSON argument template from each tool's input schema,
and shows the raw response and extracted text content. Run it while the server is up
in `--console` mode.

The form follows the standard WinForms split: **`MainForm.Designer.cs`** holds only
designer-safe `InitializeComponent` (no loops, no logic) and **`MainForm.cs`** holds
all behaviour. The `SplitContainer.SplitterDistance` is set at runtime in `OnShown`
to keep `InitializeComponent` from ever throwing.

### As a Windows Service

The service name is **`aaMcpServer`**. From an **elevated** prompt:

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

Copilot Studio reaches an MCP server through a **custom connector** whose endpoint is
marked with `x-ms-agentic-protocol: mcp-streamable-1.0`. Minimal OpenAPI (Swagger 2.0):

```yaml
swagger: '2.0'
info:
  title: aa Mcp Server
  description: Query the AVEVA Historian and ArchestrA Galaxy.
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
`http://<host>:8080/mcp`, choose **No authentication**, and the registered tools appear
on the agent.

## Galaxy (ArchestrA) tools via GRAccess

The `galaxy_*` tools talk to an ArchestrA Galaxy through the AVEVA **GRAccess** COM
library. They are **off by default** and are only registered when both:

- `Galaxy.Enabled = true` in the config, AND
- `Galaxy.Node` + `Galaxy.Name` are non-empty.

**Prerequisites.** GRAccess is registered when you install **AVEVA System Platform IDE**
(or the older Wonderware ArchestrA IDE) on the same host that runs `aaMcpServer.exe`. If
GRAccess is not installed, the project still builds and runs — the `galaxy_*` tools just
return a clear error explaining the COM library was not found.

**Late binding by design.** `Galaxy/GalaxyClient.cs` uses `Type.GetTypeFromProgID` + the
`dynamic` keyword to call GRAccess. The project therefore takes no proprietary build-time
references on GRAccess interop assemblies, and the same binary works against any
GRAccess version that registers under one of the configured ProgIDs. Method/property
names use the long-standing public surface (`QueryGalaxies`, `Galaxy.Login`,
`QueryObjectsByName`, `Tagname`, `DerivedFromName`, `Attributes`, …); if a release
renames one, the tool error message names the failing call so it can be adjusted in
`GalaxyClient.cs`.

## Security notes

- **No endpoint auth by design.** Run on a trusted/internal network or terminate
  TLS + API-key/OAuth at a reverse proxy in front of the listener.
- **Least-privilege SQL login** for the Historian tools (read-only on `Runtime`).
- **Least-privilege Galaxy user** for the Galaxy tools (read-only role).
- **Injection-safe SQL** — all tag names, timestamps, retrieval modes and configured
  identifiers are validated/escaped before being placed in SQL.

## Reference

Historian behavior follows the *AVEVA Historian Retrieval Guide* — the
`History`/`Live`/`Tag` tables and the `wwRetrievalMode`, `wwCycleCount`, `wwResolution`
time-domain extensions. Galaxy behavior follows the AVEVA GRAccess (Galaxy Repository
Access) COM API.

## License

Licensed under the **Apache License, Version 2.0**. See the [`LICENSE`](LICENSE) file
for the full text and [`NOTICE`](NOTICE) for attributions. Each source file carries an
SPDX header (`SPDX-License-Identifier: Apache-2.0`).

## Trademarks

"AVEVA", "Wonderware", "ArchestrA" and "GRAccess" are trademarks of AVEVA Group plc
and/or its subsidiaries. This project is an independent connector and is **not
affiliated with, sponsored by, or endorsed by AVEVA**. Those names are used only to
describe the systems this software interoperates with. No AVEVA software,
documentation, or other proprietary materials are included in or distributed with this
project.
