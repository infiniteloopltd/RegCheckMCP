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

## OAuth 2.1 + PKCE Authentication

The RegCheck MCP Server implements OAuth 2.1 with PKCE (Proof Key for Code Exchange) for secure,
stateless authentication. All signing is done with HMAC-SHA256; no server-side session state is required,
making it suitable for stateless hosting environments such as Google Cloud Run.

---

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/authorize` | Starts the OAuth flow; validates PKCE params and redirects to the login page |
| `GET` | `/login` | Serves the login page with an embedded signed session token |
| `POST` | `/login` | Accepts credentials, verifies them, issues a signed auth code and redirects to the client |
| `POST` | `/token` | Exchanges an auth code + code verifier for a Bearer access token |
| `*` | `/mcp` | MCP transport endpoint; requires a valid Bearer token or X-Api-Key header |

---

### Flow Overview

```
Client                        RegCheck MCP Server
  |                                  |
  |-- GET /authorize                 |
  |   ?response_type=code            |
  |   &client_id=...                 |
  |   &redirect_uri=...              |
  |   &code_challenge=...            |  <- SHA-256(code_verifier)
  |   &code_challenge_method=S256    |
  |   &state=...                     |
  |                                  |
  |        302 -> /login             |
  |            ?session_token=...    |  <- signed token carrying PKCE state
  |<---------------------------------|
  |                                  |
  |-- POST /login                    |
  |   username + password            |
  |   + session_token (hidden field) |
  |                                  |
  |        302 -> redirect_uri       |
  |            ?code=...             |  <- short-lived signed auth code
  |            &state=...            |
  |<---------------------------------|
  |                                  |
  |-- POST /token                    |
  |   code + code_verifier           |  <- proves client holds the original secret
  |                                  |
  |        200 { access_token }      |  <- Bearer token, valid 1 hour
  |<---------------------------------|
  |                                  |
  |-- POST /mcp                      |
  |   Authorization: Bearer ...      |
  |<---------------------------------|
```

---

### Secrets Configuration

Signing secrets are stored in an embedded resource file that is excluded from source control.

**1. Copy the example file:**

```
cp RegCheckMCP/Resources/secrets.json.example RegCheckMCP/Resources/secrets.json
```

**2. Edit `secrets.json` and fill in your own values:**

```json
{
  "SessionSigningKey": "...",
  "AuthCodeSigningKey": "..."
}
```

- **SessionSigningKey** — used to sign session tokens (issued at `/authorize`) and Bearer access tokens
  (issued at `/token`). Minimum 32 characters, cryptographically random.
- **AuthCodeSigningKey** — used to sign the short-lived authorization codes issued after successful
  login. Should be different from `SessionSigningKey`.

**3.** `secrets.json` is listed in `.gitignore` and will never be committed. Only `secrets.json.example`
is tracked.

> **Generating secure keys** — in PowerShell:
> ```powershell
> [Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
> ```
> Run this twice to get two independent keys.

---

### Token Lifetimes

| Token | Lifetime | Notes |
|-------|----------|-------|
| Session token | 10 minutes | Time allowed to complete the login form after `/authorize` |
| Auth code | 60 seconds | Must be exchanged at `/token` promptly after redirect |
| Access token | 1 hour | Sent as `Authorization: Bearer` on MCP requests |

---

### Credential Validation

User credentials are validated against the RegCheck API. To create or manage your account visit
[regcheck.org.uk](https://www.regcheck.org.uk/).

---

### Legacy API Key Support

The MCP endpoint also accepts the legacy `X-Api-Key` header for backwards compatibility with existing
integrations. Bearer token authentication is preferred for new clients.

```http
X-Api-Key: your-username
```

---

### Security Notes

- PKCE is **required** — the server rejects any authorization request that omits `code_challenge` or
  uses a method other than `S256`.
- Auth codes are **single-use by design** — they expire after 60 seconds and are HMAC-signed, so
  replaying a captured code will fail.
- All tokens are **stateless** — the server can be restarted or scaled horizontally without
  invalidating existing tokens, provided the signing keys in `secrets.json` remain unchanged.
- Secrets are **embedded at build time** — they are compiled into the assembly and never read from
  disk or environment variables at runtime.
- Bearer tokens encode `username:clientId:expiry` and are verified with HMAC-SHA256 on every
  request before the username is trusted.

## License

MIT

