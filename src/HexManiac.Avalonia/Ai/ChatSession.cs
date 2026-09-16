using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

public enum ChatRole { User, Assistant, Tool, System }

/// <summary>One bubble in the transcript.</summary>
public class ChatEntry {
   public ChatRole Role { get; init; }
   public string Text { get; set; }
   public bool IsError { get; init; }
}

/// <summary>
/// The conversation. Owns the transcript and hands the actual work to whichever
/// <see cref="IChatBackend"/> the panel picked.
/// </summary>
public sealed class ChatSession {
   private CancellationTokenSource cancellation;
   private readonly ChatSink sink;

   public ObservableCollection<ChatEntry> Transcript { get; } = new();
   public IChatBackend Backend { get; set; }
   public bool IsBusy { get; private set; }

   public event Action BusyChanged;

   public ChatSession() {
      sink = new ChatSink {
         AssistantText = text => Add(ChatRole.Assistant, text),
         ToolActivity = text => Add(ChatRole.Tool, text),
         Note = (text, isError) => Add(ChatRole.System, text, isError),
      };
   }

   public void Clear() {
      Backend?.Reset();
      Transcript.Clear();
   }

   public void Cancel() => cancellation?.Cancel();

   public async Task Send(string userText) {
      if (IsBusy || string.IsNullOrWhiteSpace(userText)) return;
      if (Backend == null) {
         Add(ChatRole.System, "No assistant is configured. See the note at the top of this panel.", isError: true);
         return;
      }

      Add(ChatRole.User, userText);
      SetBusy(true);
      cancellation = new CancellationTokenSource();
      try {
         await Backend.Send(userText, sink, cancellation.Token);
      } catch (OperationCanceledException) {
         Add(ChatRole.System, "Stopped.");
      } catch (AnthropicException e) {
         Add(ChatRole.System, e.Message, isError: true);
      } catch (Exception e) {
         Add(ChatRole.System, $"{e.GetType().Name}: {e.Message}", isError: true);
      } finally {
         cancellation?.Dispose();
         cancellation = null;
         SetBusy(false);
      }
   }

   private void Add(ChatRole role, string text, bool isError = false) =>
      Dispatcher.UIThread.Post(() => Transcript.Add(new ChatEntry { Role = role, Text = text, IsError = isError }));

   private void SetBusy(bool value) {
      IsBusy = value;
      Dispatcher.UIThread.Post(() => BusyChanged?.Invoke());
   }
}
