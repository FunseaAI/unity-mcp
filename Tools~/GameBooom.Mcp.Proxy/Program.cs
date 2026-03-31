using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameBooom.Mcp.Proxy;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        if (HasFlag(args, "--version"))
        {
            Console.Error.WriteLine("gamebooom-mcp-proxy 0.1.2");
            return 0;
        }

        var url = GetOption(args, "--url")
            ?? Environment.GetEnvironmentVariable("GAMEBOOOM_MCP_URL")
            ?? "http://127.0.0.1:8765/";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var endpointUri))
        {
            Console.Error.WriteLine($"Invalid --url value: {url}");
            return 2;
        }

        var timeoutSecondsText = GetOption(args, "--timeout-seconds")
            ?? Environment.GetEnvironmentVariable("GAMEBOOOM_MCP_TIMEOUT_SECONDS")
            ?? "120";
        if (!int.TryParse(timeoutSecondsText, out var timeoutSeconds) || timeoutSeconds <= 0)
        {
            Console.Error.WriteLine($"Invalid timeout value: {timeoutSecondsText}");
            return 2;
        }

        using var httpClient = CreateHttpClient(endpointUri, timeoutSeconds);
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();

        Console.Error.WriteLine($"[GameBooom MCP Proxy] Bridging stdio to {endpointUri}");

        while (true)
        {
            var request = await StdioProtocol.ReadMessageAsync(input, CancellationToken.None);
            if (request is null)
            {
                return 0;
            }

            JsonNode? requestNode = null;
            JsonNode? requestId = null;

            try
            {
                requestNode = JsonNode.Parse(request.Json);
                requestId = requestNode?["id"]?.DeepClone();
            }
            catch (JsonException)
            {
                await WriteErrorAsync(output, null, -32700, "Parse error");
                continue;
            }

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri)
                {
                    Content = new StringContent(request.Json, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(responseBody))
                {
                    await StdioProtocol.WriteMessageAsync(output, responseBody, CancellationToken.None);
                    continue;
                }

                if (response.IsSuccessStatusCode && requestId is null)
                {
                    continue;
                }

                if (response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(responseBody))
                {
                    await WriteErrorAsync(output, requestId, -32000, "Unity MCP server returned an empty response.");
                    continue;
                }

                if (requestId is not null)
                {
                    var message = string.IsNullOrWhiteSpace(responseBody)
                        ? $"Unity MCP server returned HTTP {(int)response.StatusCode}."
                        : $"Unity MCP server returned HTTP {(int)response.StatusCode}: {responseBody}";
                    await WriteErrorAsync(output, requestId, -32000, message);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameBooom MCP Proxy] {ex.Message}");
                if (requestId is not null)
                {
                    await WriteErrorAsync(output, requestId, -32000, $"Proxy transport error: {ex.Message}");
                }
            }
        }
    }

    private static HttpClient CreateHttpClient(Uri endpointUri, int timeoutSeconds)
    {
        var normalizedEndpoint = endpointUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? endpointUri
            : new Uri(endpointUri.AbsoluteUri + "/");

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        return new HttpClient(handler)
        {
            BaseAddress = normalizedEndpoint,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    private static async Task WriteErrorAsync(Stream output, JsonNode? requestId, int code, string message)
    {
        var json = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        }.ToJsonString(JsonOptions);

        await StdioProtocol.WriteMessageAsync(output, json, CancellationToken.None);
    }

    private static bool HasFlag(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("gamebooom-mcp-proxy");
        Console.Error.WriteLine("Bridges stdio MCP traffic to a local GameBooom Unity Editor HTTP MCP server.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --url <http://127.0.0.1:8765/>   Unity MCP HTTP endpoint.");
        Console.Error.WriteLine("  --timeout-seconds <120>           HTTP timeout per request.");
        Console.Error.WriteLine("  --version                         Print the proxy version.");
        Console.Error.WriteLine("  --help                            Show this help.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Environment:");
        Console.Error.WriteLine("  GAMEBOOOM_MCP_URL");
        Console.Error.WriteLine("  GAMEBOOOM_MCP_TIMEOUT_SECONDS");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };
}

internal sealed record StdioMessage(string Json);

internal static class StdioProtocol
{
    public static async Task<StdioMessage?> ReadMessageAsync(Stream input, CancellationToken ct)
    {
        var contentLength = -1;

        while (true)
        {
            var line = await ReadHeaderLineAsync(input, ct);
            if (line is null)
            {
                return contentLength >= 0
                    ? throw new EndOfStreamException("Unexpected EOF while reading MCP headers.")
                    : null;
            }

            if (line.Length == 0)
            {
                break;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = int.TryParse(value, out var parsedLength) ? parsedLength : -1;
            }
        }

        if (contentLength < 0)
        {
            throw new InvalidDataException("Missing Content-Length header.");
        }

        var payload = await ReadExactAsync(input, contentLength, ct);
        return new StdioMessage(Encoding.UTF8.GetString(payload));
    }

    public static async Task WriteMessageAsync(Stream output, string json, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");

        await output.WriteAsync(header, ct);
        await output.WriteAsync(payload, ct);
        await output.FlushAsync(ct);
    }

    private static async Task<string?> ReadHeaderLineAsync(Stream input, CancellationToken ct)
    {
        var buffer = new List<byte>(64);

        while (true)
        {
            var next = new byte[1];
            var bytesRead = await input.ReadAsync(next, ct);
            if (bytesRead == 0)
            {
                return buffer.Count == 0 ? null : DecodeLine(buffer);
            }

            if (next[0] == (byte)'\n')
            {
                return DecodeLine(buffer);
            }

            buffer.Add(next[0]);
        }
    }

    private static string DecodeLine(List<byte> buffer)
    {
        if (buffer.Count > 0 && buffer[^1] == (byte)'\r')
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static async Task<byte[]> ReadExactAsync(Stream input, int contentLength, CancellationToken ct)
    {
        var buffer = new byte[contentLength];
        var totalRead = 0;

        while (totalRead < contentLength)
        {
            var bytesRead = await input.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), ct);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected EOF while reading MCP payload.");
            }

            totalRead += bytesRead;
        }

        return buffer;
    }
}
