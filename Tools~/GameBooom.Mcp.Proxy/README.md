# GameBooom MCP Proxy

`GameBooom.Mcp.Proxy` is a lightweight stdio bridge for the local GameBooom Unity Editor MCP server.

It exists so this repository can be packaged as a publishable MCP runtime instead of only exposing a localhost HTTP endpoint from inside the Unity Editor.

<!-- mcp-name: io.github.gamebooom/unity-mcp-proxy -->

## Local usage

Start the Unity Editor MCP server first, then run:

```bash
dotnet run --project Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj -- --url http://127.0.0.1:8765/
```

You can also set `GAMEBOOOM_MCP_URL` instead of passing `--url`.

## Pack as a NuGet tool

```bash
dotnet pack Tools~/GameBooom.Mcp.Proxy/GameBooom.Mcp.Proxy.csproj -c Release
```

The generated package lands in `Tools~/GameBooom.Mcp.Proxy/nupkg/`.

## Registry goal

The intended MCP Registry server name for this proxy package is:

```text
io.github.gamebooom/unity-mcp-proxy
```

Use `Registry~/server.json.template` as the starting point when publishing the NuGet package to the official MCP Registry.
