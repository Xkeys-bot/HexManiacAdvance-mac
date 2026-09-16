using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.AvaloniaUI.Mcp;

/// <summary>
/// One tool the server offers. <paramref name="Invoke"/> is called off the UI thread and is
/// responsible for marshalling anything it needs.
/// </summary>
public record McpTool(string Name, string Title, string Description, JsonObject InputSchema, Func<JsonObject, Task<McpToolResult>> Invoke);

/// <param name="Text">Text content. Always sent, even alongside an image, so a transcript reads sensibly.</param>
/// <param name="ImageBase64">Optional image, base64 without a data: prefix.</param>
public record McpToolResult(string Text, string ImageBase64 = null, string ImageMime = null, bool IsError = false) {
   public static McpToolResult Error(string message) => new(message, IsError: true);
}

/// <summary>
/// A Model Context Protocol server, spoken over the Streamable HTTP transport, so an assistant can
/// work on the ROM that is open in the editor rather than on a copy of the file.
///
/// Implemented directly on HttpListener and System.Text.Json rather than pulling in the MCP SDK:
/// the protocol surface a tools-only server needs is small (initialize, tools/list, tools/call,
/// ping), and this project has deliberately kept its dependency list to Avalonia.
///
/// It binds to 127.0.0.1 only and rejects a request whose Origin header is not a loopback address.
/// That combination is what the MCP spec asks for to stop a web page in the user's browser from
/// driving a local server through DNS rebinding.
/// </summary>
public sealed class McpServer : IDisposable {
   public const string ProtocolVersion = "2025-06-18";
   private const string ServerName = "hexmaniac-advance";

   private readonly IReadOnlyList<McpTool> tools;
   private readonly Dictionary<string, McpTool> toolsByName;
   private HttpListener listener;
   private CancellationTokenSource cancellation;
   private string sessionId;

   public int Port { get; private set; }
   public bool IsRunning => listener?.IsListening ?? false;
   public string Url => $"http://127.0.0.1:{Port}/mcp";

   /// Raised (off the UI thread) for anything worth showing the user: start, stop, each tool call.
   public event Action<string> Log;

   public McpServer(IReadOnlyList<McpTool> tools) {
      this.tools = tools;
      toolsByName = tools.ToDictionary(tool => tool.Name);
   }

   /// <summary>
   /// Starts listening, trying <paramref name="preferredPort"/> first and walking upward if it is
   /// taken - a second copy of the editor should not fail to start a server, it should get its own.
   /// </summary>
   public void Start(int preferredPort = 8261) {
      if (IsRunning) return;
      Exception last = null;
      for (var port = preferredPort; port < preferredPort + 20; port++) {
         try {
            var candidate = new HttpListener();
            candidate.Prefixes.Add($"http://127.0.0.1:{port}/");
            candidate.Start();
            listener = candidate;
            Port = port;
            break;
         } catch (HttpListenerException e) {
            last = e;
         } catch (SocketException e) {
            last = e;
         }
      }
      if (listener == null) throw new InvalidOperationException($"Could not bind a local port: {last?.Message}");

      sessionId = Guid.NewGuid().ToString("N");
      cancellation = new CancellationTokenSource();
      Task.Run(() => AcceptLoop(cancellation.Token));
      Log?.Invoke($"MCP server listening on {Url}");
   }

   public void Stop() {
      if (listener == null) return;
      cancellation?.Cancel();
      try { listener.Stop(); } catch (ObjectDisposedException) { }
      listener = null;
      Log?.Invoke("MCP server stopped");
   }

   private async Task AcceptLoop(CancellationToken token) {
      while (!token.IsCancellationRequested && (listener?.IsListening ?? false)) {
         HttpListenerContext context;
         try {
            context = await listener.GetContextAsync();
         } catch (HttpListenerException) {
            return;  // listener stopped
         } catch (ObjectDisposedException) {
            return;
         }
         _ = Task.Run(() => HandleRequest(context), token);
      }
   }

   private async Task HandleRequest(HttpListenerContext context) {
      try {
         var origin = context.Request.Headers["Origin"];
         if (!IsLoopbackOrigin(origin)) {
            await Respond(context, 403, "text/plain", Encoding.UTF8.GetBytes("forbidden origin"));
            return;
         }

         switch (context.Request.HttpMethod) {
            case "POST":
               await HandlePost(context);
               return;
            case "DELETE":
               // The client is ending its session. There is nothing per-session to tear down.
               await Respond(context, 200, "text/plain", Array.Empty<byte>());
               return;
            case "OPTIONS":
               context.Response.AddHeader("Allow", "POST, DELETE, OPTIONS");
               await Respond(context, 204, null, Array.Empty<byte>());
               return;
            default:
               // The spec allows refusing the optional server-to-client SSE stream; clients that
               // ask for one treat 405 as "this server does not push".
               await Respond(context, 405, "text/plain", Encoding.UTF8.GetBytes("method not allowed"));
               return;
         }
      } catch (Exception e) {
         try {
            await Respond(context, 500, "text/plain", Encoding.UTF8.GetBytes(e.Message));
         } catch (Exception) {
            // the client hung up; nothing useful left to do
         }
      }
   }

   private async Task HandlePost(HttpListenerContext context) {
      string body;
      using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) {
         body = await reader.ReadToEndAsync();
      }

      JsonNode request;
      try {
         request = JsonNode.Parse(body);
      } catch (JsonException e) {
         await WriteJson(context, ErrorResponse(null, -32700, "Parse error: " + e.Message));
         return;
      }

      // A batch is an array; a single message is an object. Notifications produce no response, so
      // a batch of nothing but notifications answers 202 with an empty body.
      var messages = request is JsonArray array ? array.ToList() : new List<JsonNode> { request };
      var responses = new JsonArray();
      foreach (var message in messages) {
         var response = await Dispatch(message as JsonObject);
         if (response != null) responses.Add(response);
      }

      if (responses.Count == 0) {
         await Respond(context, 202, null, Array.Empty<byte>());
         return;
      }

      context.Response.AddHeader("Mcp-Session-Id", sessionId);
      JsonNode payload = request is JsonArray ? responses : responses[0]!.DeepClone();
      await WriteJson(context, payload);
   }

   private async Task<JsonNode> Dispatch(JsonObject message) {
      if (message == null) return ErrorResponse(null, -32600, "Invalid request");
      var method = message["method"]?.GetValue<string>();
      var id = message["id"]?.DeepClone();
      var parameters = message["params"] as JsonObject;

      // No id means a notification: acknowledge by staying silent, as JSON-RPC requires.
      if (id == null) return null;

      switch (method) {
         case "initialize":
            return Result(id, new JsonObject {
               ["protocolVersion"] = parameters?["protocolVersion"]?.GetValue<string>() ?? ProtocolVersion,
               ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
               ["serverInfo"] = new JsonObject {
                  ["name"] = ServerName,
                  ["title"] = "HexManiacAdvance",
                  ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0",
               },
               ["instructions"] =
                  "Tools act on the ROM currently open in HexManiacAdvance, including edits the user has not saved. " +
                  "Prefer table_read/table_write and run_python over raw byte access: they go through the editor's " +
                  "own model, so pointers, string encoding and table metadata stay correct. " +
                  "Every change lands in the editor's undo history, so the user can undo anything you do.",
            });

         case "ping":
            return Result(id, new JsonObject());

         case "tools/list":
            var list = new JsonArray();
            foreach (var tool in tools) {
               list.Add(new JsonObject {
                  ["name"] = tool.Name,
                  ["title"] = tool.Title,
                  ["description"] = tool.Description,
                  ["inputSchema"] = tool.InputSchema.DeepClone(),
               });
            }
            return Result(id, new JsonObject { ["tools"] = list });

         case "tools/call":
            var name = parameters?["name"]?.GetValue<string>();
            if (name == null || !toolsByName.TryGetValue(name, out var called)) {
               return ErrorResponse(id, -32602, $"Unknown tool: {name}");
            }
            var arguments = parameters?["arguments"] as JsonObject ?? new JsonObject();
            Log?.Invoke($"tools/call {name}");
            McpToolResult result;
            try {
               result = await called.Invoke(arguments);
            } catch (Exception e) {
               // A failing tool is a tool result with isError, not a protocol error: the model is
               // meant to see what went wrong and try something else.
               result = McpToolResult.Error($"{e.GetType().Name}: {e.Message}");
            }
            return Result(id, ToolResultToJson(result));

         default:
            return ErrorResponse(id, -32601, $"Method not found: {method}");
      }
   }

   private static JsonObject ToolResultToJson(McpToolResult result) {
      var content = new JsonArray {
         new JsonObject { ["type"] = "text", ["text"] = result.Text ?? string.Empty },
      };
      if (result.ImageBase64 != null) {
         content.Add(new JsonObject {
            ["type"] = "image",
            ["data"] = result.ImageBase64,
            ["mimeType"] = result.ImageMime ?? "image/png",
         });
      }
      return new JsonObject { ["content"] = content, ["isError"] = result.IsError };
   }

   private static JsonObject Result(JsonNode id, JsonNode result) =>
      new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

   private static JsonObject ErrorResponse(JsonNode id, int code, string message) =>
      new() {
         ["jsonrpc"] = "2.0",
         ["id"] = id,
         ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
      };

   /// <summary>
   /// A browser sends Origin; a local MCP client does not. Absent is fine, a loopback origin is
   /// fine, anything else is a page trying to reach into the editor and is refused.
   /// </summary>
   private static bool IsLoopbackOrigin(string origin) {
      if (string.IsNullOrEmpty(origin)) return true;
      if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
      return uri.Host is "127.0.0.1" or "localhost" or "::1" or "[::1]";
   }

   private static Task WriteJson(HttpListenerContext context, JsonNode payload) =>
      Respond(context, 200, "application/json", Encoding.UTF8.GetBytes(payload.ToJsonString()));

   private static async Task Respond(HttpListenerContext context, int status, string contentType, byte[] body) {
      context.Response.StatusCode = status;
      if (contentType != null) context.Response.ContentType = contentType;
      context.Response.ContentLength64 = body.Length;
      if (body.Length > 0) await context.Response.OutputStream.WriteAsync(body);
      context.Response.Close();
   }

   public void Dispose() => Stop();
}
