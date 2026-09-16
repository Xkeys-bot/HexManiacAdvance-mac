using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using HavenSoft.HexManiac.AvaloniaUI.Mcp;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

/// <summary>
/// Answers by calling the Anthropic Messages API directly and running the tools in this process.
///
/// This is the fallback, not the default: it spends API credits, which is a separate bill from a
/// Claude subscription. It is kept because it needs nothing installed, and because running the
/// tools in-process (no MCP round trip) is the simplest thing that can possibly work.
/// </summary>
public sealed class ApiChatBackend : IChatBackend {
   private const int MaxToolRoundTrips = 24;

   private readonly IReadOnlyList<McpTool> tools;
   private readonly JsonArray toolSchemas = new();
   private readonly JsonArray messages = new();

   public AnthropicClient Client { get; }

   public string Description => $"Anthropic API ({Client.Model}) - spends API credits";

   public ApiChatBackend(AnthropicClient client, IReadOnlyList<McpTool> tools) {
      Client = client;
      this.tools = tools;
      foreach (var tool in tools) {
         toolSchemas.Add(new JsonObject {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["input_schema"] = tool.InputSchema.DeepClone(),
         });
      }
   }

   public void Reset() => messages.Clear();

   public async Task Send(string userText, ChatSink sink, CancellationToken token) {
      messages.Add(new JsonObject {
         ["role"] = "user",
         ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = userText } },
      });

      for (var round = 0; round < MaxToolRoundTrips; round++) {
         var response = await Client.CreateMessage(messages, toolSchemas, ChatPrompts.System, token);
         var content = response["content"] as JsonArray ?? new JsonArray();

         // Record what the assistant said verbatim: the next turn needs the same history the API
         // saw, including tool_use blocks, to match tool_results against.
         messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content.DeepClone() });

         var toolUses = new List<JsonObject>();
         foreach (var block in content.OfType<JsonObject>()) {
            switch (block["type"]?.GetValue<string>()) {
               case "text":
                  var text = block["text"]?.GetValue<string>();
                  if (!string.IsNullOrWhiteSpace(text)) sink.AssistantText?.Invoke(text.Trim());
                  break;
               case "tool_use":
                  toolUses.Add(block);
                  break;
            }
         }

         if (toolUses.Count == 0) return;

         var results = new JsonArray();
         foreach (var use in toolUses) {
            token.ThrowIfCancellationRequested();
            results.Add(await RunOneTool(use, sink));
         }
         messages.Add(new JsonObject { ["role"] = "user", ["content"] = results });
      }

      sink.Note?.Invoke($"Stopped after {MaxToolRoundTrips} tool rounds. Ask again to continue.", true);
   }

   private async Task<JsonObject> RunOneTool(JsonObject use, ChatSink sink) {
      var name = use["name"]?.GetValue<string>() ?? string.Empty;
      var id = use["id"]?.GetValue<string>() ?? string.Empty;
      var input = use["input"] as JsonObject ?? new JsonObject();

      sink.ToolActivity?.Invoke(DescribeCall(name, input));

      var tool = tools.FirstOrDefault(candidate => candidate.Name == name);
      if (tool == null) return ToolResult(id, new JsonArray { TextBlock($"No such tool: {name}") }, true);

      McpToolResult result;
      try {
         result = await tool.Invoke(input);
      } catch (Exception e) {
         result = McpToolResult.Error($"{e.GetType().Name}: {e.Message}");
      }

      var blocks = new JsonArray { TextBlock(result.Text ?? string.Empty) };
      if (result.ImageBase64 != null) {
         // Lets the model actually look at the emulator, which is the point of having that tool.
         blocks.Add(new JsonObject {
            ["type"] = "image",
            ["source"] = new JsonObject {
               ["type"] = "base64",
               ["media_type"] = result.ImageMime ?? "image/png",
               ["data"] = result.ImageBase64,
            },
         });
      }
      if (result.IsError) sink.Note?.Invoke(FirstLine(result.Text), true);
      return ToolResult(id, blocks, result.IsError);
   }

   private static JsonObject ToolResult(string id, JsonArray content, bool isError) => new() {
      ["type"] = "tool_result",
      ["tool_use_id"] = id,
      ["content"] = content,
      ["is_error"] = isError,
   };

   private static JsonObject TextBlock(string text) => new() { ["type"] = "text", ["text"] = text };

   private static string DescribeCall(string name, JsonObject input) {
      if (input.Count == 0) return name;
      var arguments = input.Select(pair => $"{pair.Key}: {Shorten(pair.Value?.ToString())}");
      return $"{name}  ({string.Join(", ", arguments)})";
   }

   private static string Shorten(string value) {
      if (value == null) return "null";
      value = value.Replace("\n", " ").Trim();
      return value.Length <= 60 ? value : value[..57] + "...";
   }

   private static string FirstLine(string text) {
      if (string.IsNullOrEmpty(text)) return string.Empty;
      var newline = text.IndexOf('\n');
      return newline < 0 ? text : text[..newline];
   }
}

/// <summary>Shared wording, so both backends steer the assistant the same way.</summary>
public static class ChatPrompts {
   public const string System =
      "You are helping the user hack a Game Boy Advance Pokemon ROM from inside HexManiacAdvance, " +
      "a ROM editor. Your tools act on the ROM they currently have open, including edits they have " +
      "not saved.\n\n" +
      "Work like a careful collaborator sitting next to them:\n" +
      "- Look before you write. rom_status and list_tables are cheap; guessing at addresses is not.\n" +
      "- Prefer table_read/table_write and run_python over raw bytes: they go through the editor's " +
      "model, so pointers, text encoding and field widths stay correct.\n" +
      "- Use rom_goto so the editor's view follows what you are talking about.\n" +
      "- After a change that should be visible in game, consider emulator_boot and " +
      "emulator_screenshot to check your own work rather than assuming it worked.\n" +
      "- Everything you change is in their undo history and nothing is written to their .gba file. " +
      "Saving is their decision - say so rather than offering to save.\n" +
      "- Be concise. They can see the editor; they do not need you to narrate it.";
}
