# MCP Registry Publishing

GameBooom MCP For Unity currently runs as a local Unity Editor HTTP server.

That is great for direct localhost integrations, but it is not enough for the official MCP Registry on its own:

- `modelcontextprotocol/servers` is no longer the right place for third-party server submissions
- the MCP Registry expects either a publishable package or a public remote endpoint
- a Unity Editor server bound to `127.0.0.1` is not a public remote endpoint

To close that gap, this repository now includes a publishable stdio bridge:

- `Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj`
- `Tools~/GameBooom.Mcp.Proxy/Program.cs`
- `Registry~/server.json.template`

## What the proxy does

The proxy speaks stdio MCP to the client and forwards JSON-RPC requests to the Unity Editor HTTP MCP server running on your machine.

This gives the project a package-shaped runtime that can later be published to NuGet and then registered in the official MCP Registry as a `stdio` server.

## Local build

```bash
dotnet build Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj
dotnet pack Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj -c Release
```

## Local run

```bash
dotnet run --project Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj -- --url http://127.0.0.1:8765/
```

Or:

```bash
GAMEBOOOM_MCP_URL=http://127.0.0.1:8765/ dotnet run --project Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj
```

## Future release flow

1. Start from the Unity package release version.
2. Pack and publish `GameBooom.Mcp.Proxy` to NuGet.
3. Keep the hidden `mcp-name` marker in `Tools~/GameBooom.Mcp.Proxy/README.md` aligned with the registry server name.
4. Update `Registry~/server.json.template` with the NuGet package version.
5. Use `mcp-publisher login github`.
6. Publish with `mcp-publisher publish`.

## Suggested registry identity

```text
name: io.github.gamebooom/unity-mcp-proxy
package: GameBooom.Mcp.Proxy
transport: stdio
```
