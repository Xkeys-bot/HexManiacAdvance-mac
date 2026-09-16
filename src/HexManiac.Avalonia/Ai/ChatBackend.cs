using System;
using System.Threading;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

/// <summary>Where a chat backend reports progress as it works.</summary>
public class ChatSink {
   public Action<string> AssistantText { get; init; }
   public Action<string> ToolActivity { get; init; }
   public Action<string, bool> Note { get; init; }   // text, isError
}

/// <summary>
/// Something that can answer a message using the HexManiac tools.
///
/// Two implementations, because they bill differently and that matters more than any technical
/// difference: <see cref="ClaudeCliBackend"/> drives the user's installed Claude Code, which runs
/// on their Claude subscription, and <see cref="ApiChatBackend"/> calls the Messages API directly,
/// which spends API credits. The panel prefers the first.
/// </summary>
public interface IChatBackend {
   /// Shown in the panel so it is never a mystery which one is answering, or what it costs.
   string Description { get; }
   Task Send(string userText, ChatSink sink, CancellationToken token);
   void Reset();
}
