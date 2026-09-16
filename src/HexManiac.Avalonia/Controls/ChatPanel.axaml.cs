using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Ai;
using HavenSoft.HexManiac.AvaloniaUI.Mcp;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// The chat panel that docks on the right of the editor.
///
/// It drives a <see cref="ChatSession"/> over the same tool registry the MCP server publishes, so
/// the assistant in this panel can do exactly what an external client can - read and edit tables,
/// run automation scripts, boot the emulator and look at the screen.
/// </summary>
public partial class ChatPanel : UserControl {
   private ChatSession session;
   private IReadOnlyList<McpTool> tools;
   private Func<string> getMcpUrl;

   public ChatPanel() {
      InitializeComponent();

      ModelBox.ItemsSource = AnthropicClient.Models.Select(model => model.Label).ToList();
      ModelBox.SelectedIndex = AnthropicClient.Models.ToList().IndexOf(AnthropicClient.PreferredModel);

      InputBox.AddHandler(KeyDownEvent, InputKeyDown, RoutingStrategies.Tunnel);
   }

   /// <param name="tools">The same registry the MCP server publishes, for the in-process backend.</param>
   /// <param name="getMcpUrl">
   /// Starts the MCP server if needed and returns its URL, for the Claude Code backend - that one
   /// reaches the tools over loopback rather than in-process.
   /// </param>
   public void Attach(IReadOnlyList<McpTool> tools, Func<string> getMcpUrl) {
      this.tools = tools;
      this.getMcpUrl = getMcpUrl;
      session = new ChatSession();
      session.BusyChanged += UpdateBusyState;
      session.Transcript.CollectionChanged += TranscriptChanged;
      TranscriptList.ItemsSource = session.Transcript;
      ChooseBackend(this, null);
   }

   public void FocusInput() => InputBox.Focus();

   private void TranscriptChanged(object sender, NotifyCollectionChangedEventArgs e) {
      // Keep the newest message in view, but only when the user is already at the bottom - yanking
      // the scroll position while they read back through a long answer is maddening.
      if (TranscriptScroller.Offset.Y >= TranscriptScroller.Extent.Height - TranscriptScroller.Viewport.Height - 40) {
         Dispatcher.UIThread.Post(() => TranscriptScroller.ScrollToEnd(), DispatcherPriority.Background);
      }
   }

   private void UpdateBusyState() {
      var busy = session?.IsBusy == true;
      SendButton.IsEnabled = !busy && session?.Backend != null;
      StopButton.IsVisible = busy;
      StatusText.Text = busy ? "Working..." : session?.Backend?.Description ?? string.Empty;
   }

   private void InputKeyDown(object sender, KeyEventArgs e) {
      // Enter sends, Shift+Enter makes a new line - the convention every chat box uses. Handled on
      // the tunnel so the TextBox does not insert the newline first.
      if (e.Key != Key.Return && e.Key != Key.Enter) return;
      if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
      e.Handled = true;
      SendChat(this, null);
   }

   private async void SendChat(object sender, RoutedEventArgs e) {
      if (session == null || session.IsBusy) return;
      var text = InputBox.Text;
      if (string.IsNullOrWhiteSpace(text)) return;
      InputBox.Text = string.Empty;
      await session.Send(text);
   }

   private void StopChat(object sender, RoutedEventArgs e) => session?.Cancel();

   private void ClearChat(object sender, RoutedEventArgs e) {
      session?.Clear();
      UpdateBusyState();
   }

   /// <summary>Applies the dropdown to whichever backend is answering. Both honour it.</summary>
   private void ModelChanged(object sender, SelectionChangedEventArgs e) => ApplyModel();

   private void ApplyModel() {
      if (session?.Backend == null || ModelBox.SelectedIndex < 0) return;
      var model = AnthropicClient.Models[ModelBox.SelectedIndex];
      AnthropicClient.PreferredModel = model;
      switch (session.Backend) {
         case ClaudeCliBackend cli: cli.Model = model.CliName; break;
         case ApiChatBackend api: api.Client.Model = model.Id; break;
      }
      UpdateBusyState();
   }

   /// <summary>
   /// Picks how to answer, preferring the option that does not cost the user anything extra:
   /// their installed Claude Code (billed to their Claude subscription) over the Messages API
   /// (billed as API credits).
   /// </summary>
   private void ChooseBackend(object sender, RoutedEventArgs e) {
      if (session == null) return;

      var cli = ClaudeCliBackend.FindExecutable();
      if (cli != null) {
         var url = getMcpUrl?.Invoke();
         if (url != null) {
            session.Backend = new ClaudeCliBackend(cli, url);
            ModelBox.IsEnabled = true;
            SetupPanel.IsVisible = false;
            ApplyModel();
            return;
         }
      }

      var key = AnthropicClient.FindApiKey();
      if (key != null) {
         var index = Math.Max(0, ModelBox.SelectedIndex);
         session.Backend = new ApiChatBackend(new AnthropicClient(key, AnthropicClient.Models[index].Id), tools);
         ModelBox.IsEnabled = true;
         SetupPanel.IsVisible = false;
         ApplyModel();
         return;
      }

      session.Backend = null;
      SetupText.Text =
         "The chat panel needs one of these." + Environment.NewLine + Environment.NewLine +
         "1. Claude Code (recommended - runs on your Claude subscription, no API credits):" +
         Environment.NewLine + "    " + ClaudeCliBackend.InstallCommand + Environment.NewLine +
         "   then reopen this panel or press Re-check." + Environment.NewLine + Environment.NewLine +
         "2. An Anthropic API key, which bills separately as API credits. Set ANTHROPIC_API_KEY " +
         "before launching, or put the key on one line in:" + Environment.NewLine +
         AnthropicClient.KeyFilePath + Environment.NewLine + Environment.NewLine +
         "The editor only ever reads a key - it never writes one anywhere.";
      SetupPanel.IsVisible = true;
      UpdateBusyState();
   }
}
