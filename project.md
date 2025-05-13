## MCPBI – Local Power BI MCP Server

This starting brief is a summary of everything required to build an **MCP server** that exposes programmatic access to a Power BI-Desktop models (running on localhost) as five tools usable by any LLM agent through an MCP server. 

## What is an MCP Server

MCP (Model Context Protocol) is an open standard that allows AI applications (like LLMs) to securely and consistently connect to various data sources and tools—similar to how USB-C standardizes hardware connections.
It enables LLMs to integrate with tools and data sources via pre-built adapters.

General Architecture Overview:
MCP Hosts: Apps like Claude, IDEs, or AI tools that request data.
MCP Clients: Bridge connections to servers.
MCP Servers: Expose specific data/tools (e.g., files, APIs) via MCP.
Data Sources: Can be local (files, DBs) or remote (web services).
---

### 1. Objective (one-liner)

> **Expose a running PBIX model to LLMs through the Model Context Protocol (MCP) so they can read metadata, preview data, and evaluate DAX locally—no cloud, no extra infra.**

---

### 2. High-level architecture (text diagram)

```
┌──────────────────────────┐
│   LLM / Agent / IDE      │  ← MCP client (Claude Desktop, VS Code, etc.)
└────────────▲─────────────┘
             │  STDIO (default) or HTTP/SSE
┌────────────┴─────────────┐
│  Local MCP Server (C#)   │  ← this project
│  ─────────────────────   │
│  • Resource: schema      │--┐
│  • Tool: measure_details │  │  .NET 8
│  • Tool: list_columns    │  │
│  • Tool: preview         │  │
│  • Tool: evaluate_dax    │  │
└────────────┬─────────────┘  │
             │ ADOMD.NET & TOM│
┌────────────┴─────────────┐  │
│ Power BI Desktop (AS)    │◄─┘  localhost:<PBI_PORT>  <PBI_DB_ID>
└──────────────────────────┘
```

---

### 3. Why C# MCP SDK

* Same runtime as **TOM** (`Microsoft.AnalysisServices.Tabular`) and **ADOMD.NET**—no inter-process marshalling.
* One self-contained EXE (`dotnet publish -r win-x64 --self-contained`).
* Async/await, attribute-based MCP API (`[Resource]`, `[Tool]`).

---

### 4. Environment assumptions

```text
# Supplied by calling process (External Tools ribbon or wrapper script)
PBI_PORT=60614
PBI_DB_ID=2dad69b1-9f6b-434c-b542-8ce5e6970ea1
```

---

### 5. MCP contract

| Name                 | Type         | Parameters                        | Returns                                      |
| -------------------- | ------------ | --------------------------------- | -------------------------------------------- |
| `schema_snapshot`    | **Resource** | none                              | full table+column list (JSON)                |
| `measure_details`    | **Tool**     | `table`, `measure`                | `{ dax, description, modified }`             |
| `list_relationships` | **Resource** | none                              | array `{ from, to, cardinality, direction }` |
| `preview`            | **Tool**     | `table`, `topN` (default 20)      | rows JSON                                    |
| `evaluate_dax`       | **Tool**     | `expression` (DAX w/o `EVALUATE`) | rows JSON or `{ error }`                     |

---

### 6. Code skeleton (drop into `Program.cs`)

```csharp
using Mcp;                       // ModelContextProtocol NuGet
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using System.Data;

var server = new McpServer("pbi-local");

string ConnStr() =>
    $"Data Source=localhost:{Environment.GetEnvironmentVariable("PBI_PORT")};" +
    $"Initial Catalog={Environment.GetEnvironmentVariable("PBI_DB_ID")};" +
    "Integrated Security=SSPI;Provider=MSOLAP;";

#region Resources
[Resource("schema_snapshot")]
object Schema()
{
    using var conn = new AdomdConnection(ConnStr());
    conn.Open();
    return conn.GetSchemaDataSet("TMSCHEMA_COLUMNS").Tables[0];
}

[Resource("list_relationships")]
object Relationships()
{
    const string sql = @"SELECT f.NAME AS FromTable, fc.NAME AS FromCol,
                                t.NAME AS ToTable,   tc.NAME AS ToCol,
                                r.CARDINALITY, r.CROSSFILTER_DIRECTION
                         FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS r
                         JOIN $SYSTEM.TMSCHEMA_TABLES f  ON f.ID  = r.FROM_TABLE_ID
                         JOIN $SYSTEM.TMSCHEMA_COLUMNS fc ON fc.ID = r.FROM_COLUMN_ID
                         JOIN $SYSTEM.TMSCHEMA_TABLES t  ON t.ID  = r.TO_TABLE_ID
                         JOIN $SYSTEM.TMSCHEMA_COLUMNS tc ON tc.ID = r.TO_COLUMN_ID;";
    using var conn = new AdomdConnection(ConnStr());
    conn.Open();
    var cmd = conn.CreateCommand(); cmd.CommandText = sql;
    return new AdomdDataAdapter(cmd).Fill(new DataTable());
}
#endregion

#region Tools
[Tool("measure_details")]
object MeasureDetails(string table, string measure)
{
    using var srv = new Server(); srv.Connect(ConnStr());
    var m = srv.Databases[0].Model.Tables[table].Measures[measure];
    return new { dax = m.Expression, m.Description, m.ModifiedTime };
}

[Tool("preview")]
object Preview(string table, int topN = 20)
{
    using var conn = new AdomdConnection(ConnStr()); conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = $"EVALUATE TOPN({topN}, '{table}')";
    return new AdomdDataAdapter(cmd).Fill(new DataTable());
}

[Tool("evaluate_dax")]
object Eval(string expression)
{
    using var conn = new AdomdConnection(ConnStr()); conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = $"EVALUATE ({expression})";
    try
    {
        return new AdomdDataAdapter(cmd).Fill(new DataTable());
    }
    catch (AdomdErrorResponseException ex)
    {
        return new { error = ex.Message };
    }
}
#endregion

await server.RunAsync();      // defaults to STDIO transport
```

---

### 7. Build & package

```bash
dotnet new console -n pbi-local-mcp
cd pbi-local-mcp
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.AnalysisServices.AdomdClient.NetCore.retail.amd64
dotnet add package Microsoft.AnalysisServices.Tabular
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `bin\Release\net8.0\win-x64\publish\pbi-local.exe` (≈ 35 MB).

---

### 8. Running from Power BI (External Tools)

`ExternalTools\pbi-local.mctool.json`

```jsonc
{
  "name": "PBI-Local MCP",
  "description": "Expose local model to LLMs via MCP",
  "path": "C:\\Tools\\pbi-local.exe",
  "arguments": "",
  "launchAfterPublish": true,
  "env": {
    "PBI_PORT": "%server%",
    "PBI_DB_ID": "%database%"
  }
}
```

Click **External Tools → PBI-Local MCP** and the server starts with the correct env vars.

---

### 9. Testing & CI hints

* Use `pbi-tools test --open` to spin up a dummy PBIX and capture its port/DB ID.
* Run **integration tests** that curl the MCP server and assert JSON schema.
* Lint JSON against the MCP OpenAPI schema (bundled with the SDK).

---

### 10. Security / limits

* Runs under current Windows session → inherits Desktop’s Integrated Auth.
* No write operations implemented; TOM is read-only in this scope.
* PBIX must stay open; closing Desktop kills the local AS instance.

---

### 11. Next sprint items

1. **HTTP transport** with JWT middleware for multi-user.
2. **Write-back tools** (`create_measure`, `update_measure`) behind a feature flag.
3. **Column-level sampling** (histogram) to aid anomaly detection.
4. Dockerised test harness for CI.

---

**That’s your blueprint.**
Fork, paste, and run. Happy building!
