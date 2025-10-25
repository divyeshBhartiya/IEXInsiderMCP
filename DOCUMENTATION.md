# IEXInsider - Complete Documentation

**Version:** 2.0.0 (Refactored)
**Last Updated:** October 16, 2025
**Data Coverage:** January 2023 - September 2025 (289,214 records)

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Project Overview](#project-overview)
3. [Architecture](#architecture)
4. [API Reference](#api-reference)
5. [MCP Protocol & AI Integration](#mcp-protocol--ai-integration)
6. [Query Examples](#query-examples)
7. [Development Guide](#development-guide)
8. [Deployment](#deployment)
9. [Troubleshooting](#troubleshooting)

---

## Quick Start

### Start the Application

**Option 1: Double-click the batch file**
```bash
cd C:\POCs\IEXInsider\Release
START_IEXInsider.bat
```

**Option 2: Command Line**
```bash
cd C:\POCs\IEXInsider\IEXInsiderMCP
dotnet run
```

**Option 3: Visual Studio**
1. Open `IEXInsiderMCP.sln`
2. Press **F5**

**Access the application:**
- Web Interface: http://localhost:5000
- API Documentation: http://localhost:5000/swagger

### Try Your First Query

Open the web interface and try:
- "Show me DAM prices for 2024"
- "What are peak prices in RTM market?"
- "Compare average prices across all markets"

---

## Project Overview

### What is IEXInsider?

IEXInsider is an **MCP (Model Context Protocol) compliant AI-powered application** for analyzing Indian Energy Exchange market data. It provides:

- **289,214 market records** from 3 years (2023-2025)
- **Natural Language Processing** for intuitive queries
- **Advanced filtering** with price/volume ranges, time blocks, and market types
- **MCP Server** for AI agent integration (Claude, GPT, custom agents)
- **Statistical analysis** with standard deviation, aggregations
- **Visualizations** with automatic chart generation
- **Voice input** support
- **Full async/await** for concurrent users

### Market Data

**Market Types:**
- **DAM** - Day-Ahead Market (Brown Energy/Conventional Power)
- **GDAM** - Green Day-Ahead Market (Renewable Energy)
- **RTM** - Real-Time Market (2-hour ahead trading)

**Data Fields:**
- **MCP** - Market Clearing Price (₹/kWh)
- **MCV** - Market Clearing Volume (GW)
- **IEX Demand** - Total demand (GW)
- **IEX Supply** - Total supply (GW)
- **Time Blocks** - 96 blocks per day (15-minute intervals)

**Key Metrics:**
- Total Records: 289,214
- Date Range: Jan 2023 - Sept 2025
- Markets: 3 (DAM, GDAM, RTM)
- Time Resolution: 15 minutes (96 blocks/day)

---

## Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────────────┐
│                    WEB INTERFACE                        │
│          (Claude-like Chat + Chart.js)                  │
└──────────────────────┬──────────────────────────────────┘
                       │
                       │ HTTP/HTTPS
                       ▼
┌─────────────────────────────────────────────────────────┐
│              IEXController (Unified API)                │
│  • POST /api/iex/query      (Universal query)          │
│  • GET  /api/iex/statistics (Market stats)             │
│  • POST /api/iex/jsonrpc    (MCP protocol)             │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                   MCPServer                             │
│  • Tool Registration & Execution                        │
│  • Advanced Query Processing                            │
│  • Statistical Calculations                             │
└──────────────────────┬──────────────────────────────────┘
                       │
         ┌─────────────┴─────────────┐
         ▼                           ▼
┌──────────────────┐        ┌──────────────────┐
│ NLPQueryService  │        │ IEXDataService   │
│ (NL Processing)  │        │ (Data Access)    │
└──────────────────┘        └──────────────────┘
                                     │
                                     ▼
                            ┌──────────────────┐
                            │ IEX_Market_Data  │
                            │ (289K records)   │
                            └──────────────────┘
```

### Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Backend** | .NET 9/10 (RC) | Web API framework |
| | ASP.NET Core | MVC, Controllers, DI |
| | C# 12 | Primary language |
| **Frontend** | HTML5/CSS3/ES6+ | Web interface |
| | Chart.js 4.4.0 | Data visualization |
| | Web Speech API | Voice input |
| **Protocol** | JSON-RPC 2.0 | MCP messaging |
| | REST API | Alternative access |
| **Data** | CSV | In-memory storage |
| | LINQ | Query processing |

---

## API Reference

### REST API Endpoints

#### 1. Universal Query

**Endpoint:** `POST /api/iex/query`

**Purpose:** Single endpoint for all types of queries - natural language, structured filters, aggregations

**Request Body:**
```json
{
  "query": "string (required)",
  "filters": {
    "market_type": "DAM|GDAM|RTM",
    "year": 2023-2025,
    "start_date": "YYYY-MM-DD",
    "end_date": "YYYY-MM-DD",
    "mcp_min": 9.0,
    "mcp_max": 10.0,
    "mcv_min": 0.5,
    "mcv_max": 5.0,
    "time_blocks": ["00:00-00:15", "00:15-00:30"]
  },
  "aggregation": "average|sum|count|max|min|stddev",
  "group_by": "market_type|year|month|date|time_block",
  "limit": 100
}
```

**Examples:**

**Natural Language:**
```json
POST /api/iex/query
{
  "query": "Show me DAM prices for 2024",
  "limit": 10
}
```

**Price Range Filter:**
```json
POST /api/iex/query
{
  "query": "Get data in price range",
  "filters": {
    "market_type": "DAM",
    "year": 2023,
    "mcp_min": 9,
    "mcp_max": 10
  },
  "limit": 100
}
```

**Time Block Analysis:**
```json
POST /api/iex/query
{
  "query": "Evening peak hours analysis",
  "filters": {
    "year": 2025,
    "time_blocks": ["17:00-17:15", "17:15-17:30", "17:30-17:45", "17:45-18:00"]
  },
  "aggregation": "average"
}
```

#### 2. Statistics

**Endpoint:** `GET /api/iex/statistics?marketType={optional}`

**Purpose:** Get comprehensive market statistics including standard deviation

**Example:**
```bash
GET /api/iex/statistics
GET /api/iex/statistics?marketType=DAM
```

**Response:**
```json
{
  "MarketType": "DAM",
  "TotalRecords": 96480,
  "MCP": {
    "Average": 4.8523,
    "Max": 15.2500,
    "Min": 0.5000,
    "StdDev": 2.1547
  },
  "MCV": {
    "Average": 3.2134,
    "Max": 8.9000,
    "Min": 0.1000,
    "StdDev": 1.5432
  },
  "DateRange": {
    "Start": "2023-01-01",
    "End": "2025-09-30"
  }
}
```

#### 3. Query Suggestions

**Endpoint:** `GET /api/iex/suggestions?q={partialQuery}`

**Purpose:** Get query suggestions for autocomplete

**Example:**
```bash
GET /api/iex/suggestions?q=show
```

---

## MCP Protocol & AI Integration

### What is MCP?

**Model Context Protocol (MCP)** is a standardized protocol that allows AI agents (like Claude, GPT, or custom LLMs) to interact with external tools and data sources using JSON-RPC 2.0.

### Can IEXInsider Integrate with AI Agents?

**YES! IEXInsider is fully MCP compliant and can integrate with:**

1. **Claude Desktop App** (Anthropic)
2. **OpenAI GPT Custom Actions**
3. **Custom AI Agents** (LangChain, AutoGPT, etc.)
4. **Any MCP-compliant client**

### How AI Agents Use IEXInsider

```
┌─────────────────┐
│   AI Agent      │
│  (Claude/GPT)   │
└────────┬────────┘
         │
         │ JSON-RPC 2.0
         │
         ▼
┌─────────────────────────────────┐
│  POST /api/iex/jsonrpc          │
│  (MCP Endpoint)                 │
└────────┬────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  MCPServer                      │
│  • query_iex_data              │
│  • get_statistics              │
└─────────────────────────────────┘
```

### MCP JSON-RPC Endpoint

**Endpoint:** `POST /api/iex/jsonrpc`

**Supported Methods:**
- `tools/list` - Get available tools
- `tools/call` - Execute a tool

### MCP Tools Available

#### Tool 1: query_iex_data

**Description:** Universal query tool for IEX market data

**Input Schema:**
```json
{
  "query": "string (required)",
  "filters": {
    "market_type": "DAM|GDAM|RTM",
    "year": "integer",
    "start_date": "YYYY-MM-DD",
    "end_date": "YYYY-MM-DD",
    "mcp_min": "number",
    "mcp_max": "number",
    "mcv_min": "number",
    "mcv_max": "number",
    "time_blocks": ["array of time ranges"]
  },
  "aggregation": "average|sum|count|max|min|stddev",
  "group_by": "market_type|year|month|date|time_block",
  "limit": "integer"
}
```

#### Tool 2: get_statistics

**Description:** Get comprehensive market statistics with standard deviation

**Input Schema:**
```json
{
  "market_type": "DAM|GDAM|RTM|ALL (default)",
  "include_charts": "boolean (default: false)"
}
```

### Integration Examples

#### Example 1: Claude Desktop Integration

**Create MCP configuration:**

File: `claude_desktop_config.json`
```json
{
  "mcpServers": {
    "iex-insider": {
      "url": "http://localhost:5000/api/iex/jsonrpc",
      "description": "Indian Energy Exchange market data analysis",
      "tools": [
        "query_iex_data",
        "get_statistics"
      ]
    }
  }
}
```

**Usage in Claude:**
```
User: "What were the peak electricity prices in DAM market during 2024?"

Claude: [Uses query_iex_data tool]
Request:
{
  "query": "peak prices DAM 2024",
  "filters": {
    "market_type": "DAM",
    "year": 2024
  },
  "aggregation": "max"
}

Response: [Shows peak prices with analysis]
```

#### Example 2: OpenAI GPT Custom Action

**OpenAPI Schema:**
```yaml
openapi: 3.0.0
info:
  title: IEX Insider API
  version: 2.0.0
servers:
  - url: http://localhost:5000/api/iex
paths:
  /jsonrpc:
    post:
      operationId: queryIEXData
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                jsonrpc:
                  type: string
                  enum: ["2.0"]
                method:
                  type: string
                  enum: ["tools/call"]
                params:
                  type: object
                  properties:
                    name:
                      type: string
                      enum: ["query_iex_data", "get_statistics"]
                    arguments:
                      type: object
```

#### Example 3: Python AI Agent (LangChain)

```python
from langchain.tools import Tool
import requests

def query_iex_data(query: str, filters: dict = None) -> dict:
    """Query IEX market data"""
    response = requests.post(
        "http://localhost:5000/api/iex/jsonrpc",
        json={
            "jsonrpc": "2.0",
            "id": "1",
            "method": "tools/call",
            "params": {
                "name": "query_iex_data",
                "arguments": {
                    "query": query,
                    "filters": filters or {}
                }
            }
        }
    )
    return response.json()["result"]

# Create LangChain tool
iex_tool = Tool(
    name="IEX Market Data",
    func=query_iex_data,
    description="Query Indian Energy Exchange market data"
)

# Use in agent
from langchain.agents import initialize_agent
agent = initialize_agent(
    tools=[iex_tool],
    llm=your_llm,
    agent="zero-shot-react-description"
)

# Query
agent.run("What was the average MCP in DAM market during peak hours in 2024?")
```

#### Example 4: cURL Testing

**List available tools:**
```bash
curl -X POST http://localhost:5000/api/iex/jsonrpc \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/list",
    "params": {}
  }'
```

**Execute query:**
```bash
curl -X POST http://localhost:5000/api/iex/jsonrpc \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "2",
    "method": "tools/call",
    "params": {
      "name": "query_iex_data",
      "arguments": {
        "query": "Show peak prices in 2024",
        "filters": {
          "year": 2024
        },
        "limit": 10
      }
    }
  }'
```

### MCP Compliance

IEXInsider is **fully compliant** with MCP Specification 2025-03-26:

- ✅ JSON-RPC 2.0 protocol
- ✅ Proper error codes (-32700 to -32603)
- ✅ Tool schema definitions
- ✅ Standardized request/response format
- ✅ Async support for concurrent requests

---

## Query Examples

### Basic Queries

```
"Show me DAM prices for 2024"
"What are peak prices in RTM market?"
"Get statistics for GDAM market"
"Show me data for January 2024"
```

### Advanced Queries (Using Filters)

**Price Range Analysis:**
```
"In 2023 how many time blocks is within 9-10Rs for MCP"
```
Implementation:
```json
{
  "query": "Count time blocks in price range",
  "filters": {
    "year": 2023,
    "mcp_min": 9,
    "mcp_max": 10
  },
  "aggregation": "count"
}
```

**Time Block Analysis:**
```
"During the time block 5:00pm to 9:00pm average MCP in year 2025"
```
Implementation:
```json
{
  "query": "Evening average prices",
  "filters": {
    "year": 2025,
    "time_blocks": [
      "17:00-17:15", "17:15-17:30", "17:30-17:45", "17:45-18:00",
      "18:00-18:15", "18:15-18:30", "18:30-18:45", "18:45-19:00",
      "19:00-19:15", "19:15-19:30", "19:30-19:45", "19:45-20:00",
      "20:00-20:15", "20:15-20:30", "20:30-20:45", "20:45-21:00"
    ]
  },
  "aggregation": "average"
}
```

**Month-wise Comparison:**
```
"In year 2025, in which month the MCV was highest compared to the rest"
```
Implementation:
```json
{
  "query": "Find highest MCV month",
  "filters": {
    "year": 2025
  },
  "group_by": "month",
  "aggregation": "max"
}
```

### Visualization Queries

These queries automatically generate charts:

```
"Compare average prices across all market types"
"Chart monthly trends for DAM in 2024"
"Visualize market volumes by type"
"Plot price trends over time"
```

---

## Development Guide

### Prerequisites

- .NET 9 SDK or later
- Visual Studio 2022+ (optional)
- Git (optional)

### Project Structure

```
IEXInsider/
├── IEXInsiderMCP/
│   ├── Controllers/
│   │   └── IEXController.cs          (Unified controller)
│   ├── Services/
│   │   ├── MCPServer.cs              (MCP Server - NEW!)
│   │   ├── NLPQueryService.cs        (Natural language processing)
│   │   └── IEXDataService.cs         (Data access)
│   ├── Models/
│   │   ├── IEXMarketData.cs          (Data models)
│   │   ├── JsonRpcModels.cs          (JSON-RPC models)
│   │   └── UniversalQueryRequest.cs  (Query models)
│   ├── wwwroot/
│   │   ├── index.html                (Web interface)
│   │   └── app.js                    (Frontend logic)
│   ├── Program.cs                    (Application startup)
│   └── appsettings.json              (Configuration)
├── IEX_Market_Data.csv               (289K records)
└── DOCUMENTATION.md                  (This file)
```

### Building the Project

```bash
cd C:\POCs\IEXInsider\IEXInsiderMCP
dotnet build
```

### Running in Development

```bash
dotnet run
```

Or press **F5** in Visual Studio

### Running Tests

```bash
dotnet test
```

### Publishing for Production

**Self-contained executable:**
```bash
dotnet publish -c Release -r win-x64 --self-contained -o ../Release
```

**Framework-dependent:**
```bash
dotnet publish -c Release -o ../Release
```

### Configuration

**appsettings.json:**
```json
{
  "IEXData": {
    "CsvFilePath": "C:\\POCs\\IEXInsider\\IEX_Market_Data.csv"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Urls": "http://localhost:5000;https://localhost:5001"
}
```

### Adding New Features

**Example: Add a new MCP tool**

1. Update `MCPServer.cs`:
```csharp
public List<MCPTool> ListTools()
{
    return new List<MCPTool>
    {
        // Existing tools...
        new MCPTool
        {
            Name = "your_new_tool",
            Description = "Tool description",
            InputSchema = new Dictionary<string, object> { /* schema */ }
        }
    };
}

public async Task<MCPToolCallResponse> ExecuteToolAsync(string toolName, ...)
{
    return toolName switch
    {
        "your_new_tool" => await ExecuteYourNewTool(arguments),
        // Existing tools...
    };
}
```

2. Test with cURL or web interface

---

## Deployment

### Production Deployment Checklist

- [ ] Update `appsettings.Production.json`
- [ ] Configure HTTPS certificates
- [ ] Set up reverse proxy (IIS/Nginx)
- [ ] Configure CORS for production domains
- [ ] Enable authentication (OAuth 2.1)
- [ ] Set up monitoring (Application Insights)
- [ ] Configure rate limiting
- [ ] Back up CSV data file
- [ ] Set up logging aggregation
- [ ] Configure health checks

### Docker Deployment (Optional)

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY Release/ .
COPY IEX_Market_Data.csv /data/
ENV IEXData__CsvFilePath=/data/IEX_Market_Data.csv
EXPOSE 5000
ENTRYPOINT ["dotnet", "IEXInsiderMCP.dll"]
```

**Build and run:**
```bash
docker build -t iex-insider .
docker run -p 5000:5000 iex-insider
```

### Cloud Deployment

**Azure App Service:**
```bash
az webapp create --name iex-insider --runtime "DOTNET|9.0"
az webapp deployment source config-zip --src Release.zip
```

**AWS Elastic Beanstalk:**
```bash
eb init -p "64bit .NET Core 9.0"
eb create iex-insider-env
eb deploy
```

---

## Troubleshooting

### Common Issues

#### 1. Data Not Loading

**Error:** "Data not loaded. Call LoadDataAsync first."

**Solution:**
- Check CSV file path in `appsettings.json`
- Ensure file exists and is accessible
- Check file permissions
- Review console logs for details

#### 2. Port Already in Use

**Error:** "Failed to bind to address http://localhost:5000"

**Solution:**
```bash
# Find process using port 5000
netstat -ano | findstr :5000

# Kill the process (replace PID)
taskkill /PID <PID> /F

# Or change port in appsettings.json
"Urls": "http://localhost:8080"
```

#### 3. CORS Errors

**Error:** "Access-Control-Allow-Origin"

**Solution:** Update `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

#### 4. Slow Query Performance

**Issue:** Queries taking too long

**Solutions:**
- Add caching layer (Redis)
- Implement pagination
- Use indexes (if migrating to database)
- Optimize filters to reduce data scanned

#### 5. MCP Integration Not Working

**Issue:** AI agent can't connect

**Checklist:**
- [ ] Server is running
- [ ] Correct URL in MCP config
- [ ] JSON-RPC endpoint accessible
- [ ] No firewall blocking
- [ ] Correct tool names in requests

### Logging

**View logs in Visual Studio:**
- View → Output → Select "Debug"

**View logs in console:**
```bash
dotnet run
# Logs appear in real-time
```

**Enable detailed logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "IEXInsiderMCP": "Trace"
    }
  }
}
```

### Performance Monitoring

**Key metrics to monitor:**
- Request latency (< 100ms target)
- Memory usage (< 500MB target)
- CPU utilization
- Concurrent requests
- Error rate

---

## Appendix

### A. CSV Data Format

```csv
TYPE,Year,Date,TimeBlock,IEXDemand,IEXSupply,MCP,MCV
DAM,2023,2023-01-01,00:00-00:15,2.45,2.38,4.25,2.20
DAM,2023,2023-01-01,00:15-00:30,2.42,2.35,4.18,2.18
...
```

### B. Supported Time Blocks

96 blocks per day, each 15 minutes:
```
00:00-00:15, 00:15-00:30, 00:30-00:45, 00:45-01:00
01:00-01:15, 01:15-01:30, ...
23:30-23:45, 23:45-00:00
```

### C. Error Codes

**HTTP Status Codes:**
- 200 OK - Success
- 400 Bad Request - Invalid input
- 500 Internal Server Error - Server error

**JSON-RPC Error Codes:**
- -32700 ParseError - Invalid JSON
- -32600 InvalidRequest - Invalid request
- -32601 MethodNotFound - Method not found
- -32602 InvalidParams - Invalid parameters
- -32603 InternalError - Internal error

### D. Browser Compatibility

| Browser | Version | Supported | Notes |
|---------|---------|-----------|-------|
| Chrome | 90+ | ✅ Full | Voice input supported |
| Edge | 90+ | ✅ Full | Voice input supported |
| Firefox | 88+ | ✅ Full | No voice input |
| Safari | 14+ | ⚠️ Partial | Limited voice support |

### E. Useful Links

- MCP Specification: https://spec.modelcontextprotocol.io/
- JSON-RPC 2.0 Spec: https://www.jsonrpc.org/specification
- Chart.js Docs: https://www.chartjs.org/docs/
- .NET 9 Docs: https://learn.microsoft.com/dotnet/

---

## Support & Contact

For issues or questions:
1. Check this documentation
2. Review console logs
3. Check GitHub issues (if open source)
4. Contact development team

---

**Built with:** .NET 9, C# 12, ASP.NET Core, Chart.js
**MCP Compliant:** ✅ JSON-RPC 2.0
**AI Integration Ready:** ✅ Claude, GPT, Custom Agents
**Production Ready:** ✅ Async, Scalable, Documented

---

**End of Documentation**
