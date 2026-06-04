# RegCheck MCP Server

An MCP (Model Context Protocol) server that exposes vehicle registration lookup for 50+ countries via the [RegCheck API](https://www.regcheck.org.uk). Once connected, AI assistants such as Claude can look up vehicle details directly from a conversation.

## Live Server

```
https://regcheckmcp-526628810409.europe-west2.run.app/mcp
```

Authentication is via the `X-Api-Key` header. Your RegCheck username is your API key.

\---

## Tools

### `lookup\_vehicle\_uk`

Look up a UK vehicle registration plate. Returns make, model, colour, fuel type, MOT expiry, tax status, and engine size.

|Parameter|Type|Description|
|-|-|-|
|`registration`|string|UK plate, e.g. `AB12CDE`. Spaces are ignored.|

### `lookup\_vehicle`

Look up a vehicle registration plate for any supported non-UK country.

|Parameter|Type|Description|
|-|-|-|
|`registration`|string|Vehicle plate number.|
|`countryCode`|string|Two-letter ISO country code, e.g. `IE`, `DE`, `FR`.|
|`state`|string|State or province code. Required for AU, CA, US, PK. Leave empty for all others.|

**Supported country codes:**
`AE` `AR` `AT` `AU` `BO` `BR` `CA` `CH` `CL` `CN` `CO` `CR` `CY` `CZ` `DE` `DK` `EC` `EE` `ES` `FI` `FR` `GR` `HR` `HU` `ID` `IE` `IL` `IN` `IS` `IT` `KZ` `LK` `LT` `LV` `MT` `MX` `MY` `NG` `NL` `NO` `NZ` `OM` `PE` `PK` `PL` `PT` `RO` `RU` `SE` `SG` `SI` `SK` `TN` `TW` `UA` `US` `ZA`

\---

## Testing Locally

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
* [Node.js](https://nodejs.org/) (for MCP Inspector)
* A RegCheck API username — register at [regcheck.org.uk](https://www.regcheck.org.uk)

### 1\. Clone and run

```
git clone https://github.com/your-org/RegCheckMcp.git
cd RegCheckMcp/RegCheckMcp
dotnet run
```

By default the server listens on port `8080`. To use a different port locally:

```
set PORT=5100
dotnet run
```

You should see:

```
info: Microsoft.Hosting.Lifetime\[14]
      Now listening on: http://0.0.0.0:8080
```

### 2\. Open MCP Inspector

In a separate terminal:

```
npx @modelcontextprotocol/inspector
```

Open the URL it prints in your browser (typically `http://localhost:6274`).

### 3\. Connect to the server

In the MCP Inspector UI:

* **Transport type:** `Streamable HTTP`
* **URL:** `http://localhost:8080/mcp` (adjust port if you changed it)
* **Headers:** add `X-Api-Key` with your RegCheck username as the value
* Click **Connect**

You should see the **Tools** tab appear with `lookup\_vehicle\_uk` and `lookup\_vehicle` listed.

### 4\. Run a test lookup

Click `lookup\_vehicle\_uk`, enter a UK plate such as `AB12CDE` in the `registration` field, and click **Run**. A successful response looks like:

```json
{
  "Description": "1997 Vauxhall Corsa Breeze, 1389CC Petrol, 5DR, Manual",
  "RegistrationYear": "1997",
  "CarMake": { "CurrentTextValue": "Vauxhall" },
  "CarModel": { "CurrentTextValue": "Corsa" },
  "FuelType": { "CurrentTextValue": "Petrol" },
  "EngineSize": { "CurrentTextValue": "1389CC" }
}
```

For a non-UK lookup, click `lookup\_vehicle` and provide a plate and country code, e.g. plate `04MH8917`, country `IE`. For Australia, also provide a state, e.g. plate `BEW76P`, country `AU`, state `NSW`.

\---

## Testing Against the Live Server

Point MCP Inspector at the live URL instead:

* **URL:** `https://regcheckmcp-526628810409.europe-west2.run.app/mcp`
* **Headers:** `X-Api-Key: your-regcheck-username`

Everything else is identical to local testing.

\---

## Adding to Claude (claude.ai)

1. Go to **Settings → Connectors → Add custom connector**
2. Enter the server URL: `https://regcheckmcp-526628810409.europe-west2.run.app/mcp`
3. Under custom headers, add `X-Api-Key` with your RegCheck username
4. Save — Claude will now invoke the vehicle lookup tools automatically when you mention a registration plate in conversation

\---

## Deployment

The server is deployed on Google Cloud Run (`europe-west2`). It reads the `PORT` environment variable automatically, which Cloud Run sets to `8080`.

To deploy your own instance:

```
gcloud run deploy regcheckmcp \\
  --source . \\
  --region europe-west2 \\
  --allow-unauthenticated
```

\---

## License

MIT

