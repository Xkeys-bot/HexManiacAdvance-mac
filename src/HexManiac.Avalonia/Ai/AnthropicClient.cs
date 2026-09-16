using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

/// <summary>
/// A small client for the Anthropic Messages API - enough to run a tool-using conversation, and
/// nothing more. Written by hand rather than taking the SDK as a dependency, for the same reason
/// the MCP server was: this project keeps its package list to Avalonia.
///
/// The key is never held anywhere but memory. It is read from ANTHROPIC_API_KEY, or from a file
/// the user creates themselves; the editor never asks them to type it into a dialog and never
/// writes it anywhere.
/// </summary>
public sealed class AnthropicClient : IDisposable {
   private const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";

   /// <summary>
   /// ANTHROPIC_BASE_URL redirects the client at a gateway or proxy, the same variable Anthropic's
   /// own tools honour. Also what makes the tool loop testable without a live key.
   /// </summary>
   private static string Endpoint {
      get {
         var baseUrl = Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL");
         if (string.IsNullOrWhiteSpace(baseUrl)) return DefaultEndpoint;
         return baseUrl.TrimEnd('/') + "/v1/messages";
      }
   }
   private const string ApiVersion = "2023-06-01";

   private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(5) };
   private readonly string apiKey;

   public string Model { get; set; }
   public int MaxTokens { get; set; } = 8192;

   public AnthropicClient(string apiKey, string model) {
      this.apiKey = apiKey;
      Model = model;
   }

   /// <summary>Where the key is looked for, in order. Null when there is none.</summary>
   public static string FindApiKey() {
      var fromEnvironment = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
      if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment.Trim();
      try {
         if (File.Exists(KeyFilePath)) {
            var fromFile = File.ReadAllText(KeyFilePath).Trim();
            if (fromFile.Length > 0) return fromFile;
         }
      } catch (IOException) {
      } catch (UnauthorizedAccessException) {
      }
      return null;
   }

   public static string KeyFilePath =>
      Path.Combine(MacFileSystem.AppSupportDirectory, "anthropic-api-key.txt");

   private static string ModelFilePath =>
      Path.Combine(MacFileSystem.AppSupportDirectory, "chat-model.txt");

   /// <summary>
   /// The model the user last picked, remembered across sessions - a chat panel that forgets which
   /// model you wanted every time you open it is a chat panel you fight with. Defaults to Sonnet:
   /// capable enough for most ROM questions, and Opus is one click away for the hard ones.
   /// </summary>
   public static ChatModel PreferredModel {
      get {
         try {
            if (File.Exists(ModelFilePath)) {
               var saved = File.ReadAllText(ModelFilePath).Trim();
               var match = Models.FirstOrDefault(model => model.CliName == saved || model.Id == saved);
               if (match != null) return match;
            }
         } catch (IOException) {
         } catch (UnauthorizedAccessException) {
         }
         return Models.First(model => model.CliName == "sonnet");
      }
      set {
         try {
            File.WriteAllText(ModelFilePath, value.CliName);
         } catch (IOException) {
         } catch (UnauthorizedAccessException) {
            // remembering the choice is a convenience, not a requirement
         }
      }
   }

   /// <summary>
   /// Models offered in the chat panel.
   ///
   /// Two names each, because the two backends want different ones: the Messages API takes a full
   /// model id, while Claude Code's --model takes a short alias (and resolves it to whatever that
   /// alias currently points at, which is what a CLI user expects).
   /// </summary>
   public static IReadOnlyList<ChatModel> Models { get; } = new[] {
      new ChatModel("claude-opus-5", "opus", "Opus 5 - most capable"),
      new ChatModel("claude-sonnet-5", "sonnet", "Sonnet 5 - balanced"),
      new ChatModel("claude-haiku-4-5-20251001", "haiku", "Haiku 4.5 - fastest"),
   };

   /// <summary>
   /// One turn of the Messages API. Returns the raw response object so the caller can walk the
   /// content blocks itself - the tool loop needs the block structure, not a flattened string.
   /// </summary>
   public async Task<JsonObject> CreateMessage(JsonArray messages, JsonArray tools, string system, CancellationToken cancellation) {
      var body = new JsonObject {
         ["model"] = Model,
         ["max_tokens"] = MaxTokens,
         ["system"] = system,
         ["messages"] = messages.DeepClone(),
      };
      if (tools != null && tools.Count > 0) body["tools"] = tools.DeepClone();

      using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) {
         Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
      };
      request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
      request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      using var response = await http.SendAsync(request, cancellation);
      var text = await response.Content.ReadAsStringAsync(cancellation);

      if (!response.IsSuccessStatusCode) {
         // The API's own error text is much more useful than a status code, so surface it.
         var detail = TryReadErrorMessage(text) ?? text;
         throw new AnthropicException($"{(int)response.StatusCode} {response.ReasonPhrase}: {detail}");
      }

      return JsonNode.Parse(text) as JsonObject
         ?? throw new AnthropicException("The API returned something that was not a JSON object.");
   }

   private static string TryReadErrorMessage(string body) {
      try {
         return (JsonNode.Parse(body) as JsonObject)?["error"]?["message"]?.GetValue<string>();
      } catch (Exception) {
         return null;
      }
   }

   public void Dispose() => http.Dispose();
}

/// <param name="Id">Full model id, for the Messages API.</param>
/// <param name="CliName">Short alias, for Claude Code's --model.</param>
public record ChatModel(string Id, string CliName, string Label);

public class AnthropicException : Exception {
   public AnthropicException(string message) : base(message) { }
}
