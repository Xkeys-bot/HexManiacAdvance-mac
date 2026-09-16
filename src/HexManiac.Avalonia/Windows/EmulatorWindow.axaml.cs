using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Emulation;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Hosts a libretro GBA core so a ROM can be tested without leaving the editor.
///
/// The window asks its owner for the ROM bytes each time it boots, so "Reload ROM" always runs
/// what HexManiac currently has in memory - unsaved edits included. Nothing is written to the
/// user's .gba.
/// </summary>
public partial class EmulatorWindow : Window {
   private readonly EmulatorSession session = new();
   private Func<(byte[] Rom, string Name)> romSource;
   private DispatcherTimer statusTimer;
   private string lastKeySeen;
   private DateTime lastKeySeenAt;

   /// <summary>Keyboard layout, matching what emulators on this platform conventionally use.</summary>
   private static readonly Dictionary<Key, Libretro.JoypadButton> KeyMap = new() {
      [Key.Up] = Libretro.JoypadButton.Up,
      [Key.Down] = Libretro.JoypadButton.Down,
      [Key.Left] = Libretro.JoypadButton.Left,
      [Key.Right] = Libretro.JoypadButton.Right,
      [Key.Z] = Libretro.JoypadButton.A,
      [Key.X] = Libretro.JoypadButton.B,
      [Key.A] = Libretro.JoypadButton.L,
      [Key.S] = Libretro.JoypadButton.R,
      [Key.Enter] = Libretro.JoypadButton.Start,
      [Key.Return] = Libretro.JoypadButton.Start,
      [Key.Back] = Libretro.JoypadButton.Select,
   };

   public EmulatorWindow() {
      InitializeComponent();

      session.Faulted += OnSessionFaulted;
      session.ReloadFinished += OnReloadFinished;
      Screen.Session = session;

      // The screen is the only thing here that wants the keyboard; without this the window opens
      // with a toolbar button focused and the first key press goes to the button.
      Opened += (sender, e) => { Screen.Focus(); ShowActiveState(); };

      // Immediate, rather than waiting up to half a second for the status timer: this is the cue
      // that tells someone why their key presses are going nowhere.
      Activated += (sender, e) => { Screen.Focus(); ShowActiveState(); };
      Deactivated += (sender, e) => { session.ClearButtons(); ShowActiveState(); };

      AddHandler(KeyDownEvent, HandleGameKeyDown, RoutingStrategies.Tunnel);
      AddHandler(KeyUpEvent, HandleGameKeyUp, RoutingStrategies.Tunnel);

      statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (s, e) => UpdateStatus());
      statusTimer.Start();
   }

   /// <param name="getRom">
   /// Called on the UI thread every time the emulator boots. Returning the live model array means
   /// the emulator always starts from the editor's current state rather than a stale snapshot.
   /// </param>
   public void Attach(Func<(byte[] Rom, string Name)> getRom) {
      romSource = getRom;
      StartOrExplain();
   }

   private void StartOrExplain() {
      var rom = romSource?.Invoke();
      if (rom?.Rom == null || rom.Value.Rom.Length == 0) {
         ShowNoCore("There is no ROM open to run. Open a .gba file first, then try again.", offerBrowse: false);
         return;
      }

      var cores = EmulatorPaths.FindCores();
      if (cores.Count == 0) {
         ShowNoCore(
            "HexManiacAdvance does not ship an emulator - it borrows a libretro GBA core. " +
            "Installing RetroArch and its mGBA core is the easiest way to get one, or point this " +
            "window at a core file you already have.",
            offerBrowse: true);
         return;
      }

      TryStart(cores);
   }

   private void TryStart(IReadOnlyList<GbaCore> cores) {
      var rom = romSource.Invoke();
      var failures = new List<string>();

      foreach (var core in cores) {
         try {
            session.Start(core.Path, rom.Rom, rom.Name);
            EmulatorPaths.ConfiguredCore = core.Path;
            NoCorePanel.IsVisible = false;
            Screen.IsVisible = true;
            PopulateCoreList(core.Path);
            RefreshStateList(null);
            Title = $"Emulator - {Path.GetFileNameWithoutExtension(rom.Name)}";
            UpdateStatus();
            Screen.Focus();
            return;
         } catch (Exception e) {
            // Logged as well as collected: when a later core succeeds the list is never shown, and
            // silently falling back to a different emulator than the one the user chose is exactly
            // the kind of thing that should leave a trace.
            Console.WriteLine($"[emulator] {core.Name} ({core.Source}) failed to start: {e}");
            failures.Add($"{core.Name} ({core.Source}): {e.Message}");
         }
      }

      ShowNoCore("Found a core, but it would not start:" + Environment.NewLine +
                 string.Join(Environment.NewLine, failures), offerBrowse: true);
   }

   private void ShowNoCore(string detail, bool offerBrowse) {
      NoCoreDetail.Text = detail;
      NoCorePanel.IsVisible = true;
      BrowseCoreButton.IsVisible = CoreHelpButton.IsVisible = offerBrowse;
      Screen.IsVisible = false;
      PlayPauseButton.IsEnabled = ResetButton.IsEnabled = ReloadButton.IsEnabled = false;
      SaveStateButton.IsEnabled = LoadStateButton.IsEnabled = false;
      StatusText.Text = string.Empty;
   }

   private void UpdateStatus() {
      ShowActiveState();
      if (!session.IsRunning) return;
      var audio = session.AudioEnabled ? string.Empty : "  (no audio)";
      var note = reloadNote != null && DateTime.UtcNow < reloadNoteUntil ? $"{reloadNote}  -  " : string.Empty;
      // The core's name is not repeated here: it is in the picker immediately to the left. It used
      // to be, and the combined string was long enough that the toolbar clipped it - which took
      // the frame rate and any note with it, at the right-hand end where they were easiest to miss.
      StatusText.Text = session.IsPaused
         ? $"{note}paused{audio}"
         : $"{note}{session.MeasuredFps:0.0} fps{audio}";
      PlayPauseButton.IsEnabled = ResetButton.IsEnabled = ReloadButton.IsEnabled = true;
      SaveStateButton.IsEnabled = true;
   }

   /// <summary>
   /// Shows the last key the window received and the buttons the console is currently being told
   /// about. The two halves fail for completely different reasons - the window never getting the
   /// key means focus or a control swallowing it, the key arriving with no button behind it means
   /// the mapping or the core - and without this the only symptom either produces is "nothing
   /// happens", which is what made this take two rounds to pin down.
   /// </summary>
   /// A window that is not in front receives no keys at all, so say so where it cannot be missed.
   private void ShowActiveState() {
      if (InactiveOverlay != null) InactiveOverlay.IsVisible = !IsActive && session.IsRunning;
      UpdateInputMonitor();
   }

   private void UpdateInputMonitor() {
      if (InputMonitor == null) return;
      // A window that is not the active one gets no key events at all, and that is by far the most
      // common reason the game "ignores the keyboard" - so say it plainly instead of showing a
      // readout that is empty for a reason the reader cannot see.
      if (!IsActive) {
         InputMonitor.Text = "click the game to play";
         return;
      }
      var key = lastKeySeen == null || DateTime.UtcNow - lastKeySeenAt > TimeSpan.FromSeconds(3)
         ? "-"
         : lastKeySeen;
      // Prefer what is held right now; fall back to the last press the core sampled, because a tap
      // is four frames and this refreshes twice a second - live-only would read "no buttons" for
      // every press a person actually makes.
      var held = session.PressedButtons != 0 ? DescribeButtons(session.PressedButtons)
         : session.SecondsSinceLastLatch < 3 ? DescribeButtons(session.LastLatchedButtons)
         : "no buttons";
      InputMonitor.Text = $"key {key}  \u2022  {held}";
   }

   private static string DescribeButtons(int mask) {
      if (mask == 0) return "no buttons";
      var names = new List<string>();
      foreach (var pair in ButtonNames) {
         if ((mask & (1 << (int)pair.Value)) != 0 && !names.Contains(pair.Key)) names.Add(pair.Key);
      }
      return names.Count == 0 ? "no buttons" : string.Join("+", names);
   }

   /// <summary>
   /// Says which of the two things actually happened, because "keep place" cannot be guaranteed:
   /// a save state describes memory as the old ROM laid it out, and an edit can invalidate it.
   /// </summary>
   private void OnReloadFinished(bool keptPlace) => Dispatcher.UIThread.Post(() => {
      reloadNote = keptPlace ? "reloaded, kept your place" : "reloaded from the start";
      reloadNoteUntil = DateTime.UtcNow + TimeSpan.FromSeconds(6);
      UpdateStatus();
   });

   private string reloadNote;
   private DateTime reloadNoteUntil;

   private void OnSessionFaulted(Exception e) => Dispatcher.UIThread.Post(() => {
      ShowNoCore("The emulator core stopped with an error:" + Environment.NewLine + e.Message, offerBrowse: true);
   });

   #region Toolbar

   private void TogglePlayPause(object sender, RoutedEventArgs e) {
      if (!session.IsRunning) return;
      session.SetPaused(!session.IsPaused);
      PlayPauseButton.Content = session.IsPaused ? "Resume" : "Pause";
      UpdateStatus();
      Screen.Focus();
   }

   private void ResetGame(object sender, RoutedEventArgs e) {
      session.Reset();
      Note("restarted the core");
      ResumeForVisibleChange();
      Screen.Focus();
   }

   /// <summary>
   /// Anything that changes where the game is - reset, reload, loading a state - is invisible
   /// while the emulator is paused: the picture is whatever frame was last drawn, so the button
   /// looks like it did nothing. Resume, so the result is on screen.
   /// </summary>
   private void ResumeForVisibleChange() {
      if (!session.IsRunning || !session.IsPaused) return;
      session.SetPaused(false);
      PlayPauseButton.Content = "Pause";
      UpdateStatus();
   }

   private void ReloadRom(object sender, RoutedEventArgs e) {
      var rom = romSource?.Invoke();
      if (rom?.Rom == null) return;
      if (!session.IsRunning) { StartOrExplain(); return; }
      session.ReloadRom(rom.Value.Rom, rom.Value.Name, KeepPlaceBox.IsChecked ?? false);
      Title = $"Emulator - {Path.GetFileNameWithoutExtension(rom.Value.Name)}";
      ResumeForVisibleChange();
      Screen.Focus();
   }

   /// <summary>
   /// Named states, kept on disk per ROM. This is the practical answer to "I do not want to walk
   /// across the game to reach the thing I am editing": save the spot once, and afterwards every
   /// edit is Reload ROM (with Keep place) away from being tested right there.
   /// </summary>
   private async void SaveState(object sender, RoutedEventArgs e) {
      if (!session.IsRunning) return;
      var state = session.SaveState();
      if (state == null) { Note("the core would not give a save state"); return; }

      var dialog = new RequestTextDialog { Title = "Name this state" };
      dialog.Prompt.Text = "A name you will recognise, such as: Pallet Town, Brock's gym, route 3 grass.";
      dialog.TextBox.Text = StateBox.SelectedItem as string ?? string.Empty;
      var name = await dialog.ShowDialog<string>(this);
      if (string.IsNullOrWhiteSpace(name)) return;

      try {
         File.WriteAllBytes(EmulatorPaths.StatePath(RomKey, name.Trim()), state);
      } catch (IOException exception) {
         Note("could not write that state: " + exception.Message);
         return;
      }
      RefreshStateList(name.Trim());
      Note($"saved \"{name.Trim()}\"");
      Screen.Focus();
   }

   private void LoadState(object sender, RoutedEventArgs e) {
      if (StateBox.SelectedItem is not string name) return;
      try {
         var state = File.ReadAllBytes(EmulatorPaths.StatePath(RomKey, name));
         Note(session.LoadState(state) ? $"jumped to \"{name}\"" : "that state does not fit the ROM as it stands now");
      } catch (IOException exception) {
         Note("could not read that state: " + exception.Message);
      }
      ResumeForVisibleChange();
      Screen.Focus();
   }

   private void DeleteState(object sender, RoutedEventArgs e) {
      if (StateBox.SelectedItem is not string name) return;
      try { File.Delete(EmulatorPaths.StatePath(RomKey, name)); } catch (IOException) { }
      RefreshStateList(null);
      Screen.Focus();
   }

   private void StateSelected(object sender, SelectionChangedEventArgs e) {
      var has = StateBox.SelectedItem is string;
      LoadStateButton.IsEnabled = has;
      DeleteStateButton.IsEnabled = has;
   }

   /// Keyed on the ROM's file name: a state from one game means nothing in another.
   private string RomKey => Path.GetFileNameWithoutExtension(romSource?.Invoke().Name ?? "rom");

   private void RefreshStateList(string select) {
      var states = EmulatorPaths.SavedStates(RomKey);
      StateBox.ItemsSource = states;
      // Pre-select, so Load is usable the moment the window opens rather than needing a pick first.
      StateBox.SelectedItem = select != null && states.Contains(select) ? select : states.FirstOrDefault();
      StateSelected(this, null);
   }

   private void Note(string text) {
      reloadNote = text;
      reloadNoteUntil = DateTime.UtcNow + TimeSpan.FromSeconds(6);
      UpdateStatus();
   }

   private IReadOnlyList<GbaCore> coreChoices = Array.Empty<GbaCore>();
   private bool updatingCoreList;

   /// <summary>Lists the cores found on this machine, with the running one selected.</summary>
   private void PopulateCoreList(string runningPath) {
      updatingCoreList = true;
      coreChoices = EmulatorPaths.FindCores()
         .GroupBy(core => core.Path).Select(group => group.First()).ToList();
      CoreBox.ItemsSource = coreChoices.Select(core => core.Name).ToList();
      var index = coreChoices.ToList().FindIndex(core => core.Path == runningPath);
      CoreBox.SelectedIndex = index;
      updatingCoreList = false;
   }

   /// <summary>
   /// Remembers the choice for next launch. It cannot take effect now: a libretro core keeps global
   /// state, and loading a second one into a process that already has one segfaults - which is
   /// exactly how VBA-M first went wrong here.
   /// </summary>
   private void CoreChanged(object sender, SelectionChangedEventArgs e) {
      if (updatingCoreList || CoreBox.SelectedIndex < 0 || CoreBox.SelectedIndex >= coreChoices.Count) return;
      var chosen = coreChoices[CoreBox.SelectedIndex];
      EmulatorPaths.ConfiguredCore = chosen.Path;
      if (chosen.Path == session.CorePath) return;
      reloadNote = $"{chosen.Name} will be used next time the editor starts";
      reloadNoteUntil = DateTime.UtcNow + TimeSpan.FromSeconds(12);
      UpdateStatus();
   }

   private MemoryViewerWindow memoryViewer;

   private void OpenMemoryViewer(object sender, RoutedEventArgs e) {
      if (memoryViewer != null) { memoryViewer.Activate(); return; }
      if (!session.IsRunning) { reloadNote = "start the emulator first"; reloadNoteUntil = DateTime.UtcNow.AddSeconds(5); UpdateStatus(); return; }
      memoryViewer = new MemoryViewerWindow();
      memoryViewer.Closed += (s2, e2) => memoryViewer = null;
      memoryViewer.Attach(() => session.MemoryRegions, session.ReadMemory);
      memoryViewer.Show();
   }

   private void IntegerScalingChanged(object sender, RoutedEventArgs e) {
      Screen.IntegerScaling = IntegerScalingBox.IsChecked ?? true;
      Screen.InvalidateVisual();
   }

   private async void BrowseForCore(object sender, RoutedEventArgs e) {
      var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
         Title = "Choose a libretro GBA core",
         AllowMultiple = false,
      });
      var file = files?.FirstOrDefault();
      var path = file?.TryGetLocalPath();
      if (string.IsNullOrEmpty(path)) return;
      EmulatorPaths.ConfiguredCore = path;
      TryStart(new[] { new GbaCore(Path.GetFileName(path), path, "chosen by you") });
   }

   private void OpenCoreHelp(object sender, RoutedEventArgs e) => NativeProcess.Start(EmulatorPaths.CoreHelpUrl);

   #endregion

   #region Automation bridge (used by the MCP server)

   /// <summary>Reboots on the editor's current ROM bytes. Safe to call before the window is shown.</summary>
   public void ReloadFromEditor() => ReloadRom(this, null);

   /// <summary>The current frame as a PNG, or null if nothing is running yet.</summary>
   public byte[] CaptureFramePng() {
      if (!session.TryGetFrame(out var pixels, out var width, out var height)) return null;
      var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
      using (var frame = bitmap.Lock()) {
         unsafe {
            fixed (uint* source = pixels) {
               for (int y = 0; y < height; y++) {
                  Buffer.MemoryCopy(source + y * width, (byte*)frame.Address + y * frame.RowBytes, frame.RowBytes, width * 4);
               }
            }
         }
      }
      using var stream = new MemoryStream();
      bitmap.Save(stream);
      bitmap.Dispose();
      return stream.ToArray();
   }

   public IReadOnlyList<MemoryRegion> MemoryRegions => session.MemoryRegions;

   public byte[] ReadMemory(uint id, int offset, int length) => session.ReadMemory(id, offset, length);

   /// <summary>Button names an automation caller may use, matching what a person would say.</summary>
   private static readonly Dictionary<string, Libretro.JoypadButton> ButtonNames = new(StringComparer.OrdinalIgnoreCase) {
      ["a"] = Libretro.JoypadButton.A,
      ["b"] = Libretro.JoypadButton.B,
      ["l"] = Libretro.JoypadButton.L,
      ["r"] = Libretro.JoypadButton.R,
      ["start"] = Libretro.JoypadButton.Start,
      ["select"] = Libretro.JoypadButton.Select,
      ["up"] = Libretro.JoypadButton.Up,
      ["down"] = Libretro.JoypadButton.Down,
      ["left"] = Libretro.JoypadButton.Left,
      ["right"] = Libretro.JoypadButton.Right,
   };

   /// <summary>Returns the buttons it recognised, or null when nothing is running.</summary>
   public IReadOnlyList<string> SetButtons(string names, bool pressed) {
      if (!session.IsRunning) return null;
      var recognised = new List<string>();
      foreach (var name in names.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
         if (!ButtonNames.TryGetValue(name, out var button)) continue;
         session.SetButton(button, pressed);
         recognised.Add(name.ToLowerInvariant());
      }
      return recognised;
   }

   #endregion

   #region Input

   /// <summary>
   /// Game input is taken on the tunnelling route, not by overriding OnKeyDown.
   ///
   /// KeyDown bubbles, and by the time it reaches the Window every interested child has had its
   /// turn: a focused Button consumes Enter and Space as "click me", and Avalonia's keyboard
   /// navigation consumes the arrow keys to move focus between controls. Both mark the event
   /// handled, so an override on the Window sees nothing - which is exactly the six keys a GBA
   /// needs most. Tunnelling runs root-first, so the game gets them before anything else can.
   /// </summary>
   private void HandleGameKeyDown(object sender, KeyEventArgs e) {
      // Recorded before anything can decline the key, so the readout can tell "the window never
      // saw it" apart from "the window saw it and did not use it".
      lastKeySeen = e.Key.ToString();
      lastKeySeenAt = DateTime.UtcNow;
      UpdateInputMonitor();
      if (!session.IsRunning) return;
      if (e.Key == Key.Space) { TogglePlayPause(this, null); e.Handled = true; return; }
      if (KeyMap.TryGetValue(e.Key, out var button)) {
         session.SetButton(button, true);
         e.Handled = true;
      }
   }

   private void HandleGameKeyUp(object sender, KeyEventArgs e) {
      if (KeyMap.TryGetValue(e.Key, out var button)) {
         session.SetButton(button, false);
         e.Handled = true;
      }
   }

   /// Losing focus with a key held would leave the console holding that button down forever.
   protected override void OnLostFocus(RoutedEventArgs e) {
      base.OnLostFocus(e);
      session.ClearButtons();
   }

   #endregion

   protected override void OnClosed(EventArgs e) {
      memoryViewer?.Close();
      memoryViewer = null;
      statusTimer?.Stop();
      statusTimer = null;
      session.Faulted -= OnSessionFaulted;
      session.Dispose();
      base.OnClosed(e);
   }
}
