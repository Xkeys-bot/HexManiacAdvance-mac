using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using HavenSoft.HexManiac.AvaloniaUI.Controls;
using HavenSoft.HexManiac.AvaloniaUI.Emulation;
using HavenSoft.HexManiac.AvaloniaUI.Mcp;
using HavenSoft.HexManiac.AvaloniaUI.Project;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.AvaloniaUI.Views;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Ported from HexManiac.WPF/Windows/MainWindow.xaml.cs.
///
/// WPF declared ~25 Ctrl accelerators in Window.InputBindings; those are built here instead so
/// "Ctrl" can map to Cmd on macOS, and so the chorded Ctrl+D shortcuts can go through
/// MultiKeyGesture (Avalonia's KeyGesture is sealed and cannot express a chord).
/// </summary>
public partial class MainWindow : Window {
   private EditorViewModel editor;
   private readonly MultiKeyGestureSet chords = new();

   public EditorViewModel ViewModel => editor;

   public EditorViewModel Editor {
      get => editor;
      set {
         if (editor != null) {
            editor.PropertyChanged -= ViewModelPropertyChanged;
            editor.GotoViewModel.PropertyChanged -= GotoViewModelPropertyChanged;
            editor.GotoViewModel.MoveFocusToGoto -= FocusGotoBox;
            editor.MoveFocusToFind -= FocusFindBox;
            editor.MoveFocusToHexConverter -= FocusHexBox;
            editor.MoveFocusToPrimaryContent -= FocusPrimaryContent;
            editor.RequestDelayedWork -= DeferWork;
         }
         editor = value;
         DataContext = value;
         if (value == null) {
            Tabs.ItemsSource = null;
         } else {
            var tabs = new EditorTabs(value);
            tabs.Synced += (sender, e) => SyncSelectionFromViewModel();
            Tabs.ItemsSource = tabs;
            SyncSelectionFromViewModel();
         }
         if (editor != null) {
            editor.PropertyChanged += ViewModelPropertyChanged;
            editor.GotoViewModel.PropertyChanged += GotoViewModelPropertyChanged;
            editor.GotoViewModel.MoveFocusToGoto += FocusGotoBox;
            editor.MoveFocusToFind += FocusFindBox;
            editor.MoveFocusToHexConverter += FocusHexBox;
            editor.MoveFocusToPrimaryContent += FocusPrimaryContent;
            editor.RequestDelayedWork += DeferWork;
            BuildKeyBindings();
            FillQuickEditMenus();
            ResetFocus();
         }
      }
   }

   public IFileSystem FileSystem { get; set; }

   public MainWindow() {
      // Call the generated InitializeComponent, not AvaloniaXamlLoader directly: the generated
      // one both loads the XAML and assigns the x:Name fields.
      InitializeComponent();

      DragDrop.SetAllowDrop(this, true);
      AddHandler(DragDrop.DropEvent, OnDrop);

      Tabs.ContentTemplate = new Views.TabContentTemplate();

      AddHandler(KeyDownEvent, HandleChords, RoutingStrategies.Tunnel);
      // WPF ran the queue from PreviewMouseDown on the Window. Tunnelling keeps the same "before
      // anything else reacts to this click" ordering.
      AddHandler(PointerPressedEvent, RunDeferredActions, RoutingStrategies.Tunnel);
      Tabs.SelectionChanged += (sender, e) => SyncSelectionToViewModel();
      // WPF's OnActivated: coming back to the window while the goto panel is open should put the
      // caret back in the goto box rather than leaving the keyboard nowhere. Window.Activated is
      // the analog - OnGotFocus would also fire for every click inside the panel.
      Activated += (sender, e) => {
         if (editor?.GotoViewModel?.ControlVisible == true) FocusGotoBox();
      };
   }

   /// <summary>Port of the Window.InputBindings block; Ctrl becomes Cmd on macOS.</summary>
   private void BuildKeyBindings() {
      KeyBindings.Clear();
      var cmd = HexContent.CommandModifier;

      void Add(Key key, KeyModifiers modifiers, string path, object parameter = null) {
         var binding = new KeyBinding { Gesture = new KeyGesture(key, modifiers) };
         binding.Bind(KeyBinding.CommandProperty, new Avalonia.Data.Binding($"DataContext.{path}") { Source = this });
         if (parameter != null) binding.CommandParameter = parameter;
         KeyBindings.Add(binding);
      }

      Add(Key.N, cmd, nameof(EditorViewModel.New));
      Add(Key.O, cmd, nameof(EditorViewModel.Open));
      Add(Key.T, cmd, nameof(EditorViewModel.DuplicateCurrentTab));
      Add(Key.S, cmd, nameof(EditorViewModel.Save));
      Add(Key.S, cmd | KeyModifiers.Shift, nameof(EditorViewModel.SaveAs));
      Add(Key.A, cmd | KeyModifiers.Shift, nameof(EditorViewModel.SaveAll));
      Add(Key.E, cmd, nameof(EditorViewModel.ExportBackup));
      Add(Key.A, cmd, nameof(EditorViewModel.SelectAll));
      Add(Key.F5, KeyModifiers.None, nameof(EditorViewModel.RunFile), FileSystem);
      // F6 is the emulator's: F5 hands the *saved* file to whatever the OS runs .gba with, F6
      // boots what is in the editor right now. They are different enough to deserve their own key.
      KeyBindings.Add(new KeyBinding {
         Gesture = new KeyGesture(Key.F6),
         Command = new StubCommand { CanExecute = _ => true, Execute = _ => OpenEmulator() },
      });
      Add(Key.W, cmd, nameof(EditorViewModel.Close), FileSystem);

      Add(Key.Z, cmd, nameof(EditorViewModel.Undo));
      Add(Key.Z, cmd | KeyModifiers.Shift, nameof(EditorViewModel.Redo));
      Add(Key.Y, cmd, nameof(EditorViewModel.Redo));
      Add(Key.X, cmd, nameof(EditorViewModel.Cut));
      Add(Key.C, cmd, nameof(EditorViewModel.Copy));
      Add(Key.C, cmd | KeyModifiers.Shift, nameof(EditorViewModel.DeepCopy));
      Add(Key.V, cmd, nameof(EditorViewModel.Paste));
      Add(Key.B, cmd, nameof(EditorViewModel.Paste));
      Add(Key.D, cmd | KeyModifiers.Shift, nameof(EditorViewModel.DiffSinceLastSave));
      Add(Key.Delete, KeyModifiers.None, nameof(EditorViewModel.Delete));
      Add(Key.F, cmd, nameof(EditorViewModel.ShowFind), true);
      // WPF hung this off the search-results TabControl's InputBindings; the port has no such
      // control, and a window-level binding matches what a user expects from Escape anyway.
      Add(Key.Escape, KeyModifiers.None, nameof(EditorViewModel.HideSearchControls));
      Add(Key.F3, KeyModifiers.None, nameof(EditorViewModel.FindNext));
      Add(Key.F3, KeyModifiers.Shift, nameof(EditorViewModel.FindPrevious));
      Add(Key.G, cmd, $"{nameof(EditorViewModel.GotoViewModel)}.{nameof(GotoControlViewModel.ShowGoto)}", true);
      Add(Key.OemMinus, cmd, nameof(EditorViewModel.Back));
      Add(Key.OemMinus, cmd | KeyModifiers.Shift, nameof(EditorViewModel.Forward));
      Add(Key.R, cmd, nameof(EditorViewModel.ToggleShowAutomationPanelCommand));
      Add(Key.M, cmd, nameof(EditorViewModel.ToggleMatrix));
      Add(Key.H, cmd, nameof(EditorViewModel.ShowHexConverter), true);
      Add(Key.Escape, KeyModifiers.None, nameof(EditorViewModel.ClearError));

      // WPF: {hsv:MultiKeyGesture Ctrl+D, T} etc. Avalonia's KeyGesture cannot chord.
      chords.Add(MultiKeyGesture.Parse(editor.DisplayAsText, "Ctrl+D", "T"));
      chords.Add(MultiKeyGesture.Parse(editor.DisplayAsEventScript, "Ctrl+D", "E"));
      chords.Add(MultiKeyGesture.Parse(editor.DisplayAsSprite, "Ctrl+D", "S"));
      chords.Add(MultiKeyGesture.Parse(editor.DisplayAsColorPalette, "Ctrl+D", "C"));
   }

   private void HandleChords(object sender, KeyEventArgs e) {
      if (chords.HandleKeyDown(e)) e.Handled = true;
   }

   private void OnDrop(object sender, DragEventArgs e) {
      var files = e.Data.GetFiles();
      if (files == null || Editor == null || FileSystem == null) return;
      foreach (var item in files) {
         var path = item.TryGetLocalPath();
         if (path != null) CoreSeam.OpenFile(Editor, FileSystem, path);
      }
   }

   protected override void OnClosing(WindowClosingEventArgs e) {
      base.OnClosing(e);
      // Like the WPF MainWindow: ask each tab to close first so unsaved changes can cancel.
      if (!e.Cancel && Editor != null) {
         while (Editor.Count > 0) {
            var tab = Editor[0];
            tab.Close.Execute(FileSystem);
            if (Editor.Count > 0 && Editor[0] == tab) { e.Cancel = true; return; }
         }
         CoreSeam.SaveAppSettings(Editor);
      }
   }

   /// <summary>
   /// EditorViewModel.RequestDelayedWork hands the shell work that must not run while the model
   /// is mid-update (Core queues it from inside a change notification). WPF collected the actions
   /// and flushed them on the next mouse-down; this does the same.
   /// </summary>
   private readonly List<Action> deferredActions = new();

   private void DeferWork(object sender, Action work) => deferredActions.Add(work);

   private void RunDeferredActions(object sender, PointerPressedEventArgs e) {
      if (deferredActions.Count == 0) return;
      var copy = deferredActions.ToList();
      deferredActions.Clear();
      foreach (var action in copy) action();
   }

   private bool updatingSelection;

   /// <summary>
   /// WPF bound TabControl.SelectedIndex straight to the editor. That does not survive here:
   /// the mirror collection (EditorTabs) and the editor update in a different order, so a
   /// TwoWay binding coerces a just-set index back to the old one and new tabs never select.
   /// The two sides are therefore synced explicitly, each guarded against re-entry.
   /// </summary>
   private void SyncSelectionFromViewModel() {
      if (updatingSelection || editor == null) return;
      updatingSelection = true;
      // try/finally, not just an assignment: setting SelectedIndex builds the new tab's view
      // inline, so a fault in that view would otherwise leave the guard latched and stop every
      // later tab switch, turning one broken tab into a dead tab strip.
      try {
         if (editor.SelectedIndex >= 0 && editor.SelectedIndex < Tabs.ItemCount) Tabs.SelectedIndex = editor.SelectedIndex;
      } finally {
         updatingSelection = false;
      }
   }

   private void SyncSelectionToViewModel() {
      if (updatingSelection || editor == null) return;
      updatingSelection = true;
      try {
         if (Tabs.SelectedIndex >= 0) editor.SelectedIndex = Tabs.SelectedIndex;
      } finally {
         updatingSelection = false;
      }
   }

   private void GotoViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName != nameof(GotoControlViewModel.ControlVisible)) return;
      if (editor.GotoViewModel.ControlVisible) {
         BlurTabs();
         FocusGotoBox();
      } else {
         UnblurTabs();
         editor.GotoViewModel.ShowAutoCompleteOptions = false;
         ResetFocus();
      }
   }

   private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(EditorViewModel.SelectedIndex)) SyncSelectionFromViewModel();
      if (e.PropertyName == nameof(EditorViewModel.ShowAutomationPanel)) UpdateAutomationPanel();
      if (e.PropertyName == nameof(EditorViewModel.GotoViewModel)) return;
      if (e.PropertyName == nameof(EditorViewModel.FindControlVisible) && editor.FindControlVisible) {
         Dispatcher.UIThread.Post(() => FindBox.Focus(), DispatcherPriority.Background);
      }
      if (e.PropertyName == nameof(EditorViewModel.HexConverterVisible) && editor.HexConverterVisible) {
         Dispatcher.UIThread.Post(() => HexBox.Focus(), DispatcherPriority.Background);
      }
      if (e.PropertyName == nameof(EditorViewModel.HexConverterVisible) ||
          e.PropertyName == nameof(EditorViewModel.FindControlVisible) ||
          e.PropertyName == nameof(EditorViewModel.ShowError)) {
         ResetFocus();
      }
      // Port of EditBoxVisibilityChanged: whenever one of the overlay editors closes, the goto
      // autocomplete list has to close with it, or it outlives the box it belongs to.
      if ((e.PropertyName == nameof(EditorViewModel.HexConverterVisible) && !editor.HexConverterVisible) ||
          (e.PropertyName == nameof(EditorViewModel.FindControlVisible) && !editor.FindControlVisible)) {
         editor.GotoViewModel.ShowAutoCompleteOptions = false;
      }
   }

   #region Chat panel

   private bool chatPanelVisible;

   // Two overloads for the same reason OpenEmulatorClick has two: NativeMenuItem.Click is
   // EventHandler, Button.Click is EventHandler<RoutedEventArgs>.
   private void ToggleChatPanelClick(object sender, EventArgs e) {
      chatPanelVisible = !chatPanelVisible;
      if (sender is NativeMenuItem item) item.IsChecked = chatPanelVisible;
      UpdateChatPanel();
   }

   private void ToggleChatPanelClick(object sender, RoutedEventArgs e) => ToggleChatPanelClick(sender, (EventArgs)e);

   /// <summary>
   /// Shows or hides the right-hand chat dock. Mirrors UpdateAutomationPanel: Avalonia has no
   /// trigger that can drive a ColumnDefinition, so the widths are set here.
   /// </summary>
   private void UpdateChatPanel() {
      TabContainer.ColumnDefinitions[3].Width = new GridLength(chatPanelVisible ? ChatSplitter.Width : 0);
      TabContainer.ColumnDefinitions[4].Width = new GridLength(chatPanelVisible ? 360 : 0);
      if (!chatPanelVisible) return;
      // Built lazily: the tool registry needs an editor, and the panel is useless without one.
      if (!chatAttached && editor != null) {
         // The Claude Code backend reaches the tools over loopback rather than in-process, so the
         // panel can ask for the MCP server to be running - started on demand, not at launch.
         ChatTool.Attach(new HexManiacTools(() => this).BuildTools(), EnsureMcpServerUrl);
         chatAttached = true;
      }
      Dispatcher.UIThread.Post(() => ChatTool.FocusInput(), DispatcherPriority.Background);
   }

   private bool chatAttached;

   #endregion

   #region Project export

   /// <summary>
   /// Writes every table out as TSV, so a hack can live in version control. Asks for a folder,
   /// because this writes many files.
   /// </summary>
   private async void ExportProjectClick(object sender, EventArgs e) {
      var viewPort = SelectedEditableViewPort;
      if (viewPort == null) { FileSystem?.ShowCustomMessageBox("Open a ROM first.", showYesNoCancel: false); return; }
      var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
         Title = "Choose a folder to export the project into", AllowMultiple = false,
      });
      var path = folders?.FirstOrDefault()?.TryGetLocalPath();
      if (string.IsNullOrEmpty(path)) return;
      try {
         var result = ProjectExport.Export(viewPort.Model, path);
         FileSystem?.ShowCustomMessageBox(
            $"Exported {result.Tables:N0} tables, {result.Rows:N0} rows to:{Environment.NewLine}{path}" +
            Environment.NewLine + Environment.NewLine +
            "Commit that folder to track your hack in version control. Pointers are written for " +
            "context but are not read back on import.",
            showYesNoCancel: false);
      } catch (Exception exception) {
         FileSystem?.ShowCustomMessageBox("Export failed: " + exception.Message, showYesNoCancel: false);
      }
   }

   /// <summary>Reads an exported folder back into the open ROM, as one undoable change.</summary>
   private async void ImportProjectClick(object sender, EventArgs e) {
      var viewPort = SelectedEditableViewPort;
      if (viewPort == null) { FileSystem?.ShowCustomMessageBox("Open a ROM first.", showYesNoCancel: false); return; }
      var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
         Title = "Choose an exported project folder to import", AllowMultiple = false,
      });
      var path = folders?.FirstOrDefault()?.TryGetLocalPath();
      if (string.IsNullOrEmpty(path)) return;
      try {
         var result = ProjectExport.Import(viewPort, path);
         var notes = result.Notes.Count == 0
            ? string.Empty
            : Environment.NewLine + Environment.NewLine + "Skipped:" + Environment.NewLine +
              string.Join(Environment.NewLine, result.Notes.Take(12));
         FileSystem?.ShowCustomMessageBox(
            $"Applied {result.Fields:N0} value(s) across {result.Tables:N0} table(s)." +
            Environment.NewLine + "This is one entry in the undo history, and nothing was saved to your .gba." + notes,
            showYesNoCancel: false);
      } catch (Exception exception) {
         FileSystem?.ShowCustomMessageBox("Import failed: " + exception.Message, showYesNoCancel: false);
      }
   }

   private IEditableViewPort SelectedEditableViewPort {
      get {
         if (editor == null || editor.SelectedIndex < 0 || editor.SelectedIndex >= editor.Count) return null;
         var tab = editor[editor.SelectedIndex];
         if (tab is MapEditorViewModel map) tab = map.ViewPort;
         return tab as IEditableViewPort;
      }
   }

   #endregion

   #region Quick edits

   /// <summary>
   /// Fills the three Utilities submenus with the quick edits.
   ///
   /// These were bound through a converter, which could not work: the converter only had the list,
   /// and an IQuickEditItem is not an ICommand and has no Command property - it exposes
   /// CanRun/Run/CanRunChanged instead. So every generated item got a null Command, and macOS greys
   /// out a menu item with no command. That is why everything under Utilities except Scripts
   /// appeared disabled.
   ///
   /// Building them here instead mirrors HexManiac.WPF's FillQuickEditMenu, and gives each command
   /// the editor it needs. A NativeMenuItem cannot be x:Named, so the three parents are found by
   /// header.
   /// </summary>
   private void FillQuickEditMenus() {
      var root = NativeMenu.GetMenu(this);
      if (root == null || editor == null) return;
      Fill(root, "Pokedex", editor.QuickEditsPokedex);
      Fill(root, "Expand", editor.QuickEditsExpansion);
      Fill(root, "Misc", editor.QuickEditsMisc);
   }

   private void Fill(NativeMenu root, string header, IEnumerable<IQuickEditItem> edits) {
      var parent = FindMenuItem(root, header);
      if (parent?.Menu == null) return;
      // Add into the existing NativeMenu rather than replacing it: the collection change is
      // observed and reaches the exported NSMenu, where a wholesale assignment does not.
      parent.Menu.Items.Clear();
      foreach (var edit in edits) {
         parent.Menu.Add(new NativeMenuItem(edit.Name) {
            Command = CreateQuickEditCommand(edit),
            ToolTip = edit.Description,
         });
      }
   }

   private static NativeMenuItem FindMenuItem(NativeMenu menu, string header) {
      foreach (var entry in menu.Items) {
         if (entry is not NativeMenuItem item) continue;
         if (item.Header == header) return item;
         if (item.Menu != null && FindMenuItem(item.Menu, header) is NativeMenuItem found) return found;
      }
      return null;
   }

   /// <summary>
   /// One quick edit, as a command: enabled when the selected tab can run it, and on execution
   /// showing the description and a Run/Cancel choice before anything touches the ROM.
   /// </summary>
   private ICommand CreateQuickEditCommand(IQuickEditItem edit) {
      var command = new StubCommand {
         CanExecute = arg => {
            if (editor == null || editor.SelectedIndex < 0 || editor.SelectedIndex >= editor.Count) return false;
            var tab = editor[editor.SelectedIndex];
            if (tab is MapEditorViewModel map) tab = map.ViewPort;   // a map tab is a view onto the same ROM
            return tab is IViewPort viewPort && edit.CanRun(viewPort);
         },
         Execute = async arg => {
            var dialog = new QuickEditDialog(edit);
            await dialog.ShowDialog(this);
            if (dialog.ShouldRun) editor.RunQuickEdit(edit);
         },
      };
      edit.CanRunChanged += (sender, e) => command.CanExecuteChanged?.Invoke(command, EventArgs.Empty);
      return command;
   }

   #endregion

   #region MCP server

   private McpServer mcpServer;

   /// <summary>
   /// Starts or stops the Model Context Protocol server, which lets an assistant such as Claude
   /// read and edit the ROM that is open here - including unsaved changes - and drive the emulator
   /// to check its own work. It is off until the user asks for it: it is a local network listener
   /// with write access to their ROM, so it should never be something they did not turn on.
   /// </summary>
   private void ToggleMcpServerClick(object sender, EventArgs e) {
      // NativeMenuItem is not a Control, so x:Name gives no generated field - but the item that
      // was clicked is the sender, which is all the check mark needs.
      var item = sender as NativeMenuItem;
      if (mcpServer != null) {
         mcpServer.Dispose();
         mcpServer = null;
         if (item != null) item.IsChecked = false;
         UpdateTitle();
         return;
      }

      if (StartMcpServer(announce: true) && item != null) item.IsChecked = true;
   }

   /// <summary>
   /// Starts the MCP server. Also reachable from the --mcp launch argument, so the editor can come
   /// up already listening.
   /// </summary>
   /// <returns>Whether it is now running.</returns>
   public bool StartMcpServer(bool announce) {
      if (mcpServer != null && mcpServer.IsRunning) return true;
      try {
         mcpServer = new McpServer(new HexManiacTools(() => this).BuildTools());
         mcpServer.Start();
         UpdateTitle();
         if (announce) ShowMcpSetup();
         return true;
      } catch (Exception exception) {
         mcpServer = null;
         UpdateTitle();
         if (announce) FileSystem?.ShowCustomMessageBox("Could not start the MCP server: " + exception.Message, showYesNoCancel: false);
         return false;
      }
   }

   /// EditorViewModel.InformationMessage has a private setter and Core is not modified by this
   /// port, so the title bar carries the "a server is listening" signal instead.
   private void UpdateTitle() {
      var name = editor?.Singletons?.MetadataInfo?.VersionNumber is string version && version.Length > 0
         ? $"HexManiacAdvance ({version})"
         : "HexManiacAdvance";
      Title = mcpServer != null && mcpServer.IsRunning ? $"{name}  -  MCP on port {mcpServer.Port}" : name;
   }

   /// <summary>Starts the MCP server if it is not already up, and returns its URL (null on failure).</summary>
   private string EnsureMcpServerUrl() {
      if (mcpServer is not { IsRunning: true } && !StartMcpServer(announce: false)) return null;
      return mcpServer?.Url;
   }

   private void ShowMcpSetupClick(object sender, EventArgs e) => ShowMcpSetup();

   private void ShowMcpSetup() {
      if (mcpServer == null || !mcpServer.IsRunning) {
         FileSystem?.ShowCustomMessageBox(
            "The MCP server is not running. Turn on Tools > Claude Assistant (MCP Server) first.",
            showYesNoCancel: false);
         return;
      }

      var command = $"claude mcp add --transport http hexmaniac {mcpServer.Url}";
      FileSystem?.ShowCustomMessageBox(
         "The MCP server is running at " + mcpServer.Url + Environment.NewLine +
         Environment.NewLine +
         "To let Claude Code work on the ROM that is open here, run this once in your project:" + Environment.NewLine +
         Environment.NewLine +
         "    " + command + Environment.NewLine +
         Environment.NewLine +
         "It listens on 127.0.0.1 only. Everything an assistant changes goes into this editor's " +
         "undo history and is never written to your .gba - saving stays your decision.",
         showYesNoCancel: false,
         new ProcessModel("Copy the setup command", command));
   }

   #endregion

   #region Emulator

   private EmulatorWindow emulatorWindow;

   // Two overloads because NativeMenuItem.Click is EventHandler while Button.Click is
   // EventHandler<RoutedEventArgs>, and both call sites want the same handler name.
   private void OpenEmulatorClick(object sender, EventArgs e) => OpenEmulator();
   private void OpenEmulatorClick(object sender, RoutedEventArgs e) => OpenEmulator();

   /// <summary>
   /// Opens (or re-focuses) the emulator window and points it at the selected tab's model.
   ///
   /// The ROM is fetched through a callback rather than passed once, so every boot and every
   /// "Reload ROM" reads IDataModel.RawData as it stands at that moment - which is the whole point
   /// of an emulator inside the editor: you see your unsaved edits run.
   /// </summary>
   public void OpenEmulator() {
      if (emulatorWindow != null) {
         emulatorWindow.Activate();
         return;
      }
      emulatorWindow = new EmulatorWindow();
      emulatorWindow.Closed += (sender, e) => emulatorWindow = null;
      emulatorWindow.Attach(CurrentRom);
      // Show(), not Show(this): an owned window is pinned above the editor and follows it between
      // Spaces, but the point of testing a ROM is watching the game while you edit, side by side
      // or on a second display.
      emulatorWindow.Show();
   }

   public bool IsEmulatorOpen => emulatorWindow != null;

   /// <summary>Reboots the emulator on the editor's current bytes. No-op if it is not open.</summary>
   public void ReloadEmulatorRom() => emulatorWindow?.ReloadFromEditor();

   /// <summary>The emulator's current frame as a PNG, or null if it is not running.</summary>
   public byte[] CaptureEmulatorFrame() => emulatorWindow?.CaptureFramePng();

   /// <summary>The live memory regions the running core exposes; empty if it is not running.</summary>
   public IReadOnlyList<MemoryRegion> EmulatorMemoryRegions => emulatorWindow?.MemoryRegions ?? Array.Empty<MemoryRegion>();

   /// <summary>Reads the running game's memory; null if the emulator is not running.</summary>
   public byte[] ReadEmulatorMemory(uint id, int offset, int length) => emulatorWindow?.ReadMemory(id, offset, length);

   /// <summary>Presses or releases emulator buttons by name; null if the emulator is not running.</summary>
   public IReadOnlyList<string> SetEmulatorButtons(string names, bool pressed) =>
      emulatorWindow?.SetButtons(names, pressed);

   private (byte[] Rom, string Name) CurrentRom() {
      if (editor == null || editor.SelectedIndex < 0 || editor.SelectedIndex >= editor.Count) return (null, null);
      var tab = editor[editor.SelectedIndex];
      // A map editor tab is a view onto the same ROM, so accept either kind of tab.
      var viewPort = tab as IViewPort ?? (tab as MapEditorViewModel)?.ViewPort;
      var model = viewPort?.Model;
      if (model?.RawData == null) return (null, null);
      // Hand over the real file path, not the tab's display name. Some cores read the extension to
      // decide which system the image is for - VBA-M supports gb/gbc/gba and flatly refuses to load
      // when it cannot tell, which is why it appeared to "not work" while mGBA was happy.
      var path = !string.IsNullOrEmpty(viewPort.FullFileName) ? viewPort.FullFileName : tab.Name + ".gba";
      return (model.RawData, path);
   }

   #endregion

   #region Focus

   /// <summary>
   /// Port of MainWindow.xaml.cs FocusGotoBox / FocusTextBox. The goto box is an AngleTextBox,
   /// which only swaps in a real TextBox when it is hovered or focused, so ask it for one first.
   /// The focus call is deferred: GotoViewModel raises this the moment ControlVisible flips, and
   /// the panel is still collapsed (and therefore unfocusable) at that point.
   /// </summary>
   private void FocusGotoBox(object sender = default, EventArgs e = default) {
      Dispatcher.UIThread.Post(() => {
         if (editor?.GotoViewModel?.ControlVisible != true) return;
         var textBox = GotoBox.GetTextBox();
         if (textBox == null || textBox.IsFocused) return;
         textBox.SelectAll();
         textBox.Focus();
      }, DispatcherPriority.Background);
   }

   private void FocusFindBox(object sender, EventArgs e) => FocusOverlayBox(FindBox);
   private void FocusHexBox(object sender, EventArgs e) => FocusOverlayBox(HexBox);
   private void FocusPrimaryContent(object sender, EventArgs e) => FocusPrimaryContent();

   private static void FocusOverlayBox(TextBox box) {
      Dispatcher.UIThread.Post(() => {
         if (box == null || !box.IsEffectivelyVisible) return;
         box.SelectAll();
         box.Focus();
      }, DispatcherPriority.Background);
   }

   /// When every overlay textbox is closed, hand the keyboard back to the tab content.
   private void ResetFocus() {
      if (editor == null) return;
      if (editor.HexConverterVisible || editor.FindControlVisible || editor.ShowError) return;
      if (editor.GotoViewModel.ControlVisible) return;
      FocusPrimaryContent();
   }

   /// <summary>
   /// WPF walked the visual tree for a HexContent or MapTab belonging to the selected tab.
   /// Avalonia hands back the realized container for the selected index, so search inside that.
   /// </summary>
   private void FocusPrimaryContent() {
      if (Tabs.SelectedIndex < 0 || Tabs.SelectedIndex >= Tabs.ItemCount) return;
      if (Tabs.ContainerFromIndex(Tabs.SelectedIndex) is not Control container) return;
      var descendants = container.GetVisualDescendants().OfType<Control>().ToList();
      var hex = descendants.OfType<HexContent>().FirstOrDefault();
      if (hex != null) {
         // Do not steal the keyboard from the anchor editor: it sits above the hex content and
         // the user is typing an anchor name into it.
         var anchorTextBox = descendants.OfType<TextBox>().FirstOrDefault(box => box.Name == "AnchorTextBox");
         if (anchorTextBox != null && anchorTextBox.IsFocused) return;
         hex.Focus();
         return;
      }
      descendants.OfType<MapTab>().FirstOrDefault()?.Focus();
   }

   #endregion

   #region Goto blur + focus animation

   private static readonly TimeSpan FastTime = TimeSpan.FromSeconds(.75);

   /// <summary>
   /// WPF applied a BlurEffect to the tabs and faded a translucent sheet over them while the
   /// goto panel was open, animating the blur radius in. Avalonia 11 has the same BlurEffect on
   /// Visual.Effect, but no BeginAnimation, so the radius runs through an Animation instead.
   /// </summary>
   private void BlurTabs() {
      var effect = new BlurEffect { Radius = 0 };
      Tabs.Effect = effect;
      new Avalonia.Animation.Animation {
         Duration = FastTime,
         Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
         Children = {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(BlurEffect.RadiusProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(BlurEffect.RadiusProperty, 5d) } },
         },
      }.RunAsync(effect);
      new Avalonia.Animation.Animation {
         Duration = FastTime,
         Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
         Children = {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, .7d) } },
         },
      }.RunAsync(GotoBackground);
   }

   private void UnblurTabs() {
      if (editor?.GotoViewModel?.ControlVisible == true) return;
      Tabs.Effect = null;
   }

   /// <summary>
   /// A tab asked the shell to draw attention to one of its controls (ViewPort.FocusToolPanel,
   /// MapTutorialViewModel steps). WPF routed that through a RoutedCommand; here the views raise
   /// a FocusElement event and TabContentTemplate hands it to this method.
   /// </summary>
   public void AnimateFocus(Control element) {
      if (!IsActive || element == null) return;
      Dispatcher.UIThread.Post(() => DispatchAnimation(element), DispatcherPriority.ApplicationIdle);
   }

   private void DispatchAnimation(Control element) {
      if (ReferenceEquals(element, GotoBox) && editor?.GotoViewModel?.ShowAll != true) return;
      var point = element.TranslatePoint(new Avalonia.Point(), ContentPanel);
      if (point == null) return;
      var target = point.Value;

      FocusAnimationElement.IsVisible = true;
      var transform = new TranslateTransform();
      FocusAnimationElement.RenderTransform = transform;

      var endX = -ContentPanel.Bounds.Width + target.X + element.Bounds.Width;
      var animation = new Avalonia.Animation.Animation {
         Duration = FastTime,
         Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
         Children = {
            new KeyFrame { Cue = new Cue(0d), Setters = {
               new Setter(TranslateTransform.XProperty, endX / 2),
               new Setter(TranslateTransform.YProperty, target.Y / 2),
            } },
            new KeyFrame { Cue = new Cue(1d), Setters = {
               new Setter(TranslateTransform.XProperty, endX),
               new Setter(TranslateTransform.YProperty, target.Y),
            } },
         },
      };
      var sizeAnimation = new Avalonia.Animation.Animation {
         Duration = FastTime,
         Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
         Children = {
            new KeyFrame { Cue = new Cue(0d), Setters = {
               new Setter(WidthProperty, ContentPanel.Bounds.Width / 2),
               new Setter(HeightProperty, ContentPanel.Bounds.Height / 2),
            } },
            new KeyFrame { Cue = new Cue(1d), Setters = {
               new Setter(WidthProperty, element.Bounds.Width),
               new Setter(HeightProperty, element.Bounds.Height),
            } },
         },
      };
      animation.RunAsync(transform);
      sizeAnimation.RunAsync(FocusAnimationElement).ContinueWith(_ => Dispatcher.UIThread.Post(() => {
         FocusAnimationElement.IsVisible = false;
         FocusAnimationElement.RenderTransform = null;
      }));
   }

   #endregion

   /// <summary>
   /// WPF opened the Python panel by widening two collapsed columns from a property change.
   /// Avalonia has no Trigger for a ColumnDefinition, so the same sizing is applied here.
   /// </summary>
   private void UpdateAutomationPanel() {
      var show = editor?.ShowAutomationPanel == true;
      TabContainer.ColumnDefinitions[1].Width = new GridLength(show ? PythonToolSplitter.Width : 0);
      TabContainer.ColumnDefinitions[2].Width = new GridLength(show ? 300 : 0);
      if (show) {
         Dispatcher.UIThread.Post(() => PythonTool.InputBox.Focus(), DispatcherPriority.Background);
      }
   }

   #region Tab drag-reorder

   private bool tabCaptured;

   private void TabMouseDown(object sender, PointerPressedEventArgs e) {
      if (sender is not Control element) return;
      var properties = e.GetCurrentPoint(element).Properties;

      // Middle-click closes the tab. WPF declared this as a MouseBinding on the tab header
      // (MouseAction="MiddleClick"); Avalonia has no MouseBinding, so the gesture is read off the
      // pointer here.
      if (properties.IsMiddleButtonPressed) {
         if (element.DataContext is ITabContent tab) tab.Close.Execute(FileSystem);
         e.Handled = true;
         return;
      }

      if (!properties.IsLeftButtonPressed) return;
      e.Pointer.Capture(element);
      tabCaptured = true;
   }

   /// <summary>
   /// Middle-click dismisses the message and error bars, as WPF's MouseBindings on those panels
   /// did. Both are wired to this one handler; which command to run comes from the sender.
   /// </summary>
   private void DismissBarOnMiddleClick(object sender, PointerPressedEventArgs e) {
      if (sender is not Control element) return;
      if (!e.GetCurrentPoint(element).Properties.IsMiddleButtonPressed) return;
      if (editor == null) return;
      if (ReferenceEquals(sender, MessagePanel)) editor.ClearMessage.Execute();
      else editor.ClearError.Execute();
      e.Handled = true;
   }

   /// <summary>
   /// If the pointer has dragged the tab through more than half of the next tab, swap the tabs
   /// horizontally. The "more than half" metric comes from WPF and exists because tabs differ in
   /// width: a smaller threshold makes a narrow tab flicker as it passes a wide one.
   /// </summary>
   private void TabMouseMove(object sender, PointerEventArgs e) {
      if (!tabCaptured || sender is not Control element || editor == null) return;

      var index = editor.SelectedIndex;
      if (index < 0) return;
      var leftWidth = index > 0 ? TabHeaderWidth(index - 1) : double.PositiveInfinity;
      var rightWidth = index < editor.Count - 1 ? TabHeaderWidth(index + 1) : double.PositiveInfinity;
      var offset = e.GetPosition(element).X;

      if (offset < -leftWidth / 2) {
         editor.SwapTabs(index, index - 1);
      } else if (offset > element.Bounds.Width + rightWidth / 2) {
         editor.SwapTabs(index, index + 1);
      }
   }

   private void TabMouseUp(object sender, PointerReleasedEventArgs e) {
      if (!tabCaptured) return;
      tabCaptured = false;
      e.Handled = true;
      e.Pointer.Capture(null);
   }

   /// WPF walked the visual tree for the TabTextBlock whose DataContext was the neighbouring tab.
   /// Avalonia hands the container straight back from the index.
   private double TabHeaderWidth(int index) {
      if (Tabs.ContainerFromIndex(index) is not Control container) return double.PositiveInfinity;
      foreach (var descendant in container.GetVisualDescendants()) {
         if (descendant is TextBlock { Name: "TabTextBlock" } block) return block.Bounds.Width;
      }
      return double.PositiveInfinity;
   }

   #endregion

   #region Developer menu (shown only with --dev-menu)

   private void DeveloperRaiseAssert(object sender, EventArgs e) => Debug.Assert(false, "Intentional Assert");

   private void DeveloperThrowArgumentOutOfRangeException(object sender, EventArgs e) {
      var list = new List<int>();
      var number = list[13];
   }

   private void DeveloperThrowAggregateException(object sender, EventArgs e) {
      var task = System.Threading.Tasks.Task.Factory.StartNew(() => throw new NotImplementedException());
      task.Wait();
   }

   private void DeveloperWriteDebug(object sender, EventArgs e) => Debug.WriteLine("Debug");

   private void DeveloperWriteTrace(object sender, EventArgs e) => Trace.WriteLine("Trace");

   private void DeveloperRunGarbageCollection(object sender, EventArgs e) => GC.Collect();

   private void DeveloperUpdateDocs(object sender, EventArgs e) => ViewModel.Singletons.ExportReadableScriptReference(ViewModel);

   private void DeveloperReloadMetadata(object sender, EventArgs e) {
      if (ViewModel.SelectedTab is ViewPort tab) tab.ConsiderReload(FileSystem);
   }

   #endregion

   private void ShowThemeSelector(object sender, EventArgs e) {
      var selector = new ThemeSelector { DataContext = editor.Theme };
      selector.Show(this);
   }

   private void AboutClick(object sender, EventArgs e) =>
      new AboutWindow(editor.Singletons.MetadataInfo).ShowDialog(this);

   private void UpdateClick(object sender, EventArgs e) => Launch(App.ReleaseUrl);

   /// WPF bound this with {hsv:MethodCommand CalculateHashes}; a NativeMenuItem has no
   /// DataContext for a MethodCommand to bind against, so it calls the view model directly.
   private void CalculateHashesClick(object sender, EventArgs e) => editor?.CalculateHashes();

   /// WPF used NativeProcess.Start; on macOS that is `open`.
   private void WebLink(object sender, EventArgs e) {
      if (sender is NativeMenuItem item && item.CommandParameter is string url) Launch(url);
   }

   private static void Launch(string url) =>
      Process.Start(new ProcessStartInfo("open", $"\"{url}\"") { UseShellExecute = false });
}
