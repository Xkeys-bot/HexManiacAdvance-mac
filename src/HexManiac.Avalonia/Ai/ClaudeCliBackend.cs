using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

/// <summary>
/// Answers by running the user's own Claude Code installation in headless mode.
///
/// This exists because the Messages API bills separately: a user with a Claude subscription should
/// not have to buy API credits to use the chat panel. Claude Code signs in with that same
/// subscription, so spawning it costs nothing extra.
///
/// The tools do not come from this process. Claude Code is pointed at HexManiacAdvance's own MCP
/// server over loopback, so it gets the same fourteen tools an external client would - which is
/// also why the panel makes sure that server is listening before the first message.
/// </summary>
public sealed class ClaudeCliBackend : IChatBackend {
   private readonly string executable;
   private readonly string mcpUrl;
   private string sessionId;

   /// <summary>Passed to --model. Null lets Claude Code use whatever its own default is.</summary>
   public string Model { get; set; }

   public string Description =>
      $"Claude Code{(Model == null ? string.Empty : " / " + Model)} - uses your Claude subscription, not API credits";

   public ClaudeCliBackend(string executable, string mcpUrl) {
      this.executable = executable;
      this.mcpUrl = mcpUrl;
   }

   public void Reset() => sessionId = null;

   /// <summary>Where Claude Code puts itself, in the order worth checking. Null if not installed.</summary>
   public static string FindExecutable() {
      var candidates = new List<string>();
      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      candidates.Add(Path.Combine(home, ".claude", "local", "claude"));
      candidates.Add(Path.Combine(home, ".local", "bin", "claude"));
      candidates.Add("/opt/homebrew/bin/claude");
      candidates.Add("/usr/local/bin/claude");

      var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
      foreach (var directory in path.Split(':', StringSplitOptions.RemoveEmptyEntries)) {
         candidates.Add(Path.Combine(directory, "claude"));
      }

      foreach (var candidate in candidates) {
         try {
            if (File.Exists(candidate)) return candidate;
         } catch (Exception) {
            // an unreadable PATH entry is not worth failing over
         }
      }
      return null;
   }

   public const string InstallCommand = "npm install -g @anthropic-ai/claude-code";

   public async Task Send(string userText, ChatSink sink, CancellationToken token) {
      // A config file rather than a flag value, because the URL carries a port that moves.
      var configPath = Path.Combine(MacFileSystem.AppSupportDirectory, "chat-mcp-config.json");
      var config = new JsonObject {
         ["mcpServers"] = new JsonObject {
            ["hexmaniac"] = new JsonObject { ["type"] = "http", ["url"] = mcpUrl },
         },
      };
      File.WriteAllText(configPath, config.ToJsonString());

      var start = new ProcessStartInfo {
         FileName = executable,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         RedirectStandardInput = true,
         UseShellExecute = false,
         WorkingDirectory = MacFileSystem.AppSupportDirectory,
      };
      start.ArgumentList.Add("-p");
      start.ArgumentList.Add(userText);
      start.ArgumentList.Add("--output-format");
      start.ArgumentList.Add("stream-json");
      start.ArgumentList.Add("--verbose");                 // stream-json in print mode requires it
      start.ArgumentList.Add("--mcp-config");
      start.ArgumentList.Add(configPath);
      // Pre-approve this editor's own tools and nothing else, so headless mode does not stall on a
      // permission prompt no one can see. Every one of them is undoable and none writes to disk.
      start.ArgumentList.Add("--allowedTools");
      start.ArgumentList.Add("mcp__hexmaniac");
      if (Model != null) {
         start.ArgumentList.Add("--model");
         start.ArgumentList.Add(Model);
      }
      if (sessionId != null) {
         start.ArgumentList.Add("--resume");
         start.ArgumentList.Add(sessionId);
      }

      using var process = new Process { StartInfo = start };
      var errors = new StringBuilder();
      process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errors.AppendLine(e.Data); };

      try {
         process.Start();
      } catch (Exception e) {
         sink.Note?.Invoke($"Could not run {executable}: {e.Message}", true);
         return;
      }

      process.BeginErrorReadLine();
      process.StandardInput.Close();

      using (token.Register(() => { try { process.Kill(true); } catch (Exception) { } })) {
         string line;
         while ((line = await process.StandardOutput.ReadLineAsync(token)) != null) {
            HandleEvent(line, sink);
         }
         await process.WaitForExitAsync(token);
      }

      if (process.ExitCode != 0 && !token.IsCancellationRequested) {
         var detail = errors.ToString().Trim();
         sink.Note?.Invoke(
            detail.Length > 0 ? detail : $"Claude Code exited with code {process.ExitCode}.", true);
      }
   }

   /// <summary>
   /// One line of Claude Code's stream-json output. Unknown event types are ignored on purpose:
   /// the stream gains new ones over time and an editor panel should not break when it does.
   /// </summary>
   private void HandleEvent(string line, ChatSink sink) {
      JsonObject entry;
      try {
         entry = JsonNode.Parse(line) as JsonObject;
      } catch (Exception) {
         return;
      }
      if (entry == null) return;

      var id = entry["session_id"]?.GetValue<string>();
      if (id != null) sessionId = id;   // remembered so the next message continues this conversation

      switch (entry["type"]?.GetValue<string>()) {
         case "assistant":
            foreach (var block in (entry["message"]?["content"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()) {
               switch (block["type"]?.GetValue<string>()) {
                  case "text":
                     var text = block["text"]?.GetValue<string>();
                     if (!string.IsNullOrWhiteSpace(text)) sink.AssistantText?.Invoke(text.Trim());
                     break;
                  case "tool_use":
                     sink.ToolActivity?.Invoke(DescribeCall(block));
                     break;
               }
            }
            break;
         case "result":
            if (entry["is_error"]?.GetValue<bool>() == true) {
               sink.Note?.Invoke(entry["result"]?.GetValue<string>() ?? "Claude Code reported an error.", true);
            }
            break;
      }
   }

   private static string DescribeCall(JsonObject block) {
      var name = block["name"]?.GetValue<string>() ?? "tool";
      if (name.StartsWith("mcp__hexmaniac__", StringComparison.Ordinal)) name = name["mcp__hexmaniac__".Length..];
      var input = block["input"] as JsonObject;
      if (input == null || input.Count == 0) return name;
      var arguments = input.Select(pair => $"{pair.Key}: {Shorten(pair.Value?.ToString())}");
      return $"{name}  ({string.Join(", ", arguments)})";
   }

   private static string Shorten(string value) {
      if (value == null) return "null";
      value = value.Replace("\n", " ").Trim();
      return value.Length <= 60 ? value : value[..57] + "...";
   }
}
