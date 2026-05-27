# Adding a New MCP Tool

This guide walks through extending `aaMcpServer` with a new tool that Microsoft
Copilot (or any MCP client) can invoke. The project ships with a single demo tool,
`his_search_tags` (see `src/aaMcpServer/Tools/SearchTagsTool.cs`), which is the
canonical example to copy.

## Mental model

A "tool" is a named operation Copilot can call with structured arguments. Each
tool is a C# class implementing the `ITool` interface:

```
public interface ITool
{
    string  Name        { get; }   // unique tool name (e.g. "his_get_live_values")
    string  Description { get; }   // natural-language description; the LLM reads this
    JObject InputSchema { get; }   // JSON Schema describing the "arguments" object
    string  Execute(JObject arguments);  // do the work, return text for the model
}
```

Tools are registered in `AppHost.Start()`. The MCP server advertises every
registered tool via `tools/list` and routes incoming `tools/call` requests to the
matching tool's `Execute` method.

## Naming conventions

Use a prefix that groups related tools:

| Prefix | Source / domain | Example |
|--------|-----------------|---------|
| `his_` | AVEVA Historian (SQL) | `his_search_tags`, `his_get_live_values` |
| `gr_`  | ArchestrA Galaxy (GRAccess) | `gr_list_objects` |

Names are lowercase `snake_case`. Keep them short — the LLM uses the name plus
the description to choose tools, so the name should be self-explanatory.

## Quick start (3 minutes)

1. Copy `src/aaMcpServer/Tools/SearchTagsTool.cs` to `Tools/MyNewTool.cs`.
2. Rename the class, the `Name` property, the `Description`, and the SQL.
3. Adjust `InputSchema` to describe your arguments.
4. Open `src/aaMcpServer/aaMcpServer.csproj` and add the new file to the
   `<Compile>` ItemGroup:
   ```xml
   <Compile Include="Tools\MyNewTool.cs" />
   ```
5. Open `src/aaMcpServer/AppHost.cs` and register the tool:
   ```csharp
   registry.Add(new MyNewTool(client, cfg));
   ```
6. Rebuild. Restart the server. Connect with the test client → your tool appears
   in the dropdown.

## Detailed walkthrough

### Step 1 — define the tool class

Create `src/aaMcpServer/Tools/MyNewTool.cs`. Use the SPDX header (one is on every
existing source file).

```csharp
// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : <Your Name>
//  Date        : <YYYY-MM-DD>
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    public sealed class MyNewTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;

        public MyNewTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client;
            _cfg = cfg;
        }

        public string Name => "his_my_new_tool";

        public string Description =>
            "Single-sentence summary of what this tool does, followed by 1-2 " +
            "sentences explaining when the LLM should call it. The LLM uses " +
            "this text to choose between tools, so make it crisp.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact tag name.",
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "Max rows to return (default 100).",
                },
            },
            ["required"] = new JArray { "tagName" },
        };

        public string Execute(JObject args)
        {
            var tagName = Args.GetRequiredString(args, "tagName");
            var limit = SqlSanitize.ClampInt(Args.GetInt(args, "limit", 100), 1, 1000);

            var sql = "SELECT TOP (" + limit + ") TagName, Value FROM Live " +
                      "WHERE TagName = " + SqlSanitize.QuoteLiteral(tagName);

            var result = _client.Run(sql, limit);
            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatTable(result, Name, _cfg.HistorianTimesAreUtc,
                    "No rows.")
                : result.ToText("No rows.");
        }
    }
}
```

### Step 2 — register the tool

In `src/aaMcpServer/AppHost.cs`, find the `registry.Add(...)` block in `Start()`
and add your tool:

```csharp
var registry = new ToolRegistry();
registry.Add(new SearchTagsTool(client, cfg));
registry.Add(new MyNewTool(client, cfg));   // ← add here
```

### Step 3 — list the file in the csproj

`aaMcpServer.csproj` is an old-style (non-SDK) project, so files are not picked
up automatically — you must list them explicitly. Open
`src/aaMcpServer/aaMcpServer.csproj`, find the `<Compile>` ItemGroup and add:

```xml
<Compile Include="Tools\MyNewTool.cs" />
```

Keep the list alphabetised for sanity.

### Step 4 — rebuild and test

```powershell
dotnet build aaMcpServer.sln -c Release
```

Then run the server (`aaMcpServer.exe --console`) and the test client. The new
tool should appear in the dropdown with the description and argument template
you defined.

## Helper APIs

### `Tools/Args.cs` — reading arguments

```csharp
string s   = Args.GetString(args, "name");                  // optional, null if absent
string s   = Args.GetRequiredString(args, "name");          // throws ToolException if missing
int    n   = Args.GetInt(args, "limit", 100);               // with fallback
bool   ok  = Args.HasValue(args, "optional");
List<string> tags = Args.GetStringArray(args, "tagNames");          // accepts array or comma-separated
List<string> tags = Args.GetRequiredStringArray(args, "tagNames");  // throws if empty
```

### `Historian/SqlSanitize.cs` — safe SQL building

The Historian's OLE-DB-style extension queries don't reliably accept
`SqlParameter`, so we build SQL with literal values. Always escape user input.

```csharp
SqlSanitize.QuoteLiteral("O'Brien")       // → 'O''Brien'   (escapes single quote)
SqlSanitize.QuoteDateTime(value, "start") // validates + formats yyyy-MM-dd HH:mm:ss.fff
SqlSanitize.RetrievalMode("avg")          // canonicalises to "Average"; throws on unknown
SqlSanitize.Identifier(name, "Alarms.Source") // validates a table/column from config
SqlSanitize.TagNameInList(tagNames)       // → "'A', 'B'"   (quoted, comma-separated)
SqlSanitize.ClampInt(value, 1, 500)       // bounded integer
```

### `Historian/HistorianClient.cs` — running queries

```csharp
QueryResult r = _client.Run(sql, maxRows);
QueryResult r = _client.Run(sql, maxRows, sqlParameters);  // when you can use parameters
```

Returns a `QueryResult` containing `Columns`, `Rows` and `Truncated`. Connection
opens and closes per call; no lifetime to manage.

### `Historian/QueryResult.cs` — the result shape

```csharp
result.Columns        // List<string> column names
result.Rows           // List<object[]> rows aligned to Columns
result.Truncated      // true if maxRows clipped the result
result.ToText(emptyMessage)   // verbose pipe-table rendering
```

### `Output/CompactFormatter.cs` — token-efficient rendering

```csharp
CompactFormatter.FormatTable(result, toolName, cfg.HistorianTimesAreUtc, emptyMsg);
// → CSV with short header, epoch_ms timestamps for any DateTime cells

CompactFormatter.FormatLive(result, cfg.HistorianTimesAreUtc, emptyMsg);
// → one row per tag, drops constant Quality column

CompactFormatter.FormatHistory(result, mode, cfg.HistorianTimesAreUtc, emptyMsg);
// → groups by tag; for uniform-interval modes the row timestamp is implicit
//    (header carries t0 + dt); ~70% smaller than the pipe-table on Cyclic queries
```

To honour the user's preferred format (compact vs table), every tool should
delegate to `SearchTagsTool.PickFormat(args, _cfg)`:

```csharp
return SearchTagsTool.PickFormat(args, _cfg) == "compact"
    ? CompactFormatter.FormatTable(result, Name, _cfg.HistorianTimesAreUtc, "...")
    : result.ToText("...");
```

This respects the per-call `format` argument first, then falls back to the
server's `Output.Format` config default.

### `Mcp/ITool.cs` — `ToolException`

```csharp
throw new ToolException("Invalid input: ...");
```

The MCP layer catches this and returns it as a tool result with `isError: true`.
The message goes back to the model. Use it for any user-facing error (invalid
argument values, "not found", auth failures, etc.). Unhandled exceptions are
also caught but show up with a generic "Tool execution error" prefix — prefer
`ToolException` for clarity.

## Input Schema reference

The `InputSchema` property returns a JSON Schema (draft-07 style) object
describing the `arguments` payload. Copilot uses it to generate the call. Keep
it small and well-described.

| Type | Schema fragment |
|------|-----------------|
| string | `{ "type": "string", "description": "...", "enum": [...] (optional) }` |
| integer | `{ "type": "integer", "description": "..." }` |
| number | `{ "type": "number" }` |
| boolean | `{ "type": "boolean" }` |
| array of strings | `{ "type": "array", "items": { "type": "string" } }` |

Required fields go in the top-level `required` array:

```csharp
["required"] = new JArray { "tagName", "startTime" }
```

Every tool should also include the `format` argument so users can override the
default output mode:

```csharp
["format"] = new JObject
{
    ["type"] = "string",
    ["enum"] = new JArray { "compact", "table" },
    ["description"] = "Output format. Default = server's Output.Format ('compact').",
}
```

## Worked example: `his_get_live_values`

Goal: read the latest current value + quality for one or more tags from the
Historian `Live` table.

### Tool class — `src/aaMcpServer/Tools/GetLiveValuesTool.cs`

```csharp
using System.Collections.Generic;
using aaMcpServer.Historian;
using aaMcpServer.Mcp;
using aaMcpServer.Output;
using Newtonsoft.Json.Linq;

namespace aaMcpServer.Tools
{
    public sealed class GetLiveValuesTool : ITool
    {
        private readonly HistorianClient _client;
        private readonly ServiceConfig _cfg;

        public GetLiveValuesTool(HistorianClient client, ServiceConfig cfg)
        {
            _client = client; _cfg = cfg;
        }

        public string Name => "his_get_live_values";

        public string Description =>
            "Get the latest current value and quality for one or more tags from " +
            "the AVEVA Historian Live table. Use his_search_tags first to find " +
            "the exact tag names.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tagNames"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Exact tag names.",
                },
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "compact", "table" },
                    ["description"] = "Output format. Default = Output.Format ('compact').",
                },
            },
            ["required"] = new JArray { "tagNames" },
        };

        public string Execute(JObject args)
        {
            List<string> tags = Args.GetRequiredStringArray(args, "tagNames");
            var inList = SqlSanitize.TagNameInList(tags);

            var sql =
                "SELECT TagName, DateTime, vValue AS Value, Quality, QualityDetail " +
                "FROM Live WHERE TagName IN (" + inList + ") ORDER BY TagName";

            var result = _client.Run(sql, _cfg.MaxRows);

            return SearchTagsTool.PickFormat(args, _cfg) == "compact"
                ? CompactFormatter.FormatLive(result, _cfg.HistorianTimesAreUtc,
                    "No live values found for the requested tag(s).")
                : result.ToText("No live values found for the requested tag(s).");
        }
    }
}
```

### Register and list

```csharp
// AppHost.cs
registry.Add(new GetLiveValuesTool(client, cfg));
```

```xml
<!-- aaMcpServer.csproj -->
<Compile Include="Tools\GetLiveValuesTool.cs" />
```

Rebuild, restart the server, refresh the test client → `his_get_live_values`
appears with `tagNames` and `format` in the schema.

## Testing your tool

Use `aaMcpServer.TestClient` (the WinForms harness shipped in the same
solution):

1. Run `aaMcpServer.exe --console` so you can see live logs.
2. Run `aaMcpServer.TestClient.exe` (Debug or Release).
3. Click **Connect / List Tools** — the new tool appears in the dropdown.
4. Pick it. The arguments box pre-fills a JSON template from your `InputSchema`.
5. Edit and click **Call Tool**. Response pane shows the JSON-RPC request that
   went out and the raw response plus the extracted text.

For automated testing, the `Mcp` namespace is transport-agnostic — you can
instantiate `ToolRegistry`, register your tool, and call `Execute(args)`
directly in a unit test without standing up the HTTP server.

## Common pitfalls

- **Tool doesn't appear in `tools/list`.** You forgot to register it in
  `AppHost.Start()` or forgot to add it to the `<Compile>` list in the csproj.
- **Compiler can't find your class.** You renamed the class but kept the old
  filename, or vice versa. Visual Studio will auto-fix; on the command line
  you'll see a CS0246 / CS0103 error.
- **"Invalid format 'X'" at runtime.** `SearchTagsTool.PickFormat` validates
  the `format` argument; only `compact` or `table` are accepted.
- **SQL injection-style failures.** Always run user input through
  `SqlSanitize.QuoteLiteral` / `QuoteDateTime` / `TagNameInList` / `Identifier`
  before placing it in a SQL string. Never concatenate raw user input.
- **DateTime confusion.** SQL Server returns Unspecified-kind DateTimes; the
  formatter converts to epoch ms via local→UTC by default. Flip
  `Historian.TimesAreUtc = true` in `App.config` if your Historian returns UTC.
- **Description is too long.** The LLM has to read the description on every
  call. Keep it focused on *what the tool does* and *when to call it* — not
  how it works internally.
- **Forgetting to handle the empty-result case.** Pass a sensible
  `emptyMessage` to `Format*`/`ToText` so the model doesn't see a bare table
  header on a zero-row result.

## Where things live

```
src/aaMcpServer/
├── AppHost.cs              ← register tools here
├── App.config              ← runtime settings (HTTP, SQL, logging, output format)
├── Historian/
│   ├── HistorianClient.cs  ← SQL execution
│   ├── QueryResult.cs      ← row/column container
│   └── SqlSanitize.cs      ← injection-safe SQL helpers
├── Output/
│   └── CompactFormatter.cs ← token-efficient rendering
├── Mcp/
│   ├── ITool.cs            ← the interface you implement
│   ├── McpServer.cs        ← protocol dispatcher (don't usually touch)
│   └── ToolRegistry.cs
└── Tools/
    ├── Args.cs             ← argument extraction helpers
    └── SearchTagsTool.cs   ← canonical example — copy this
```
