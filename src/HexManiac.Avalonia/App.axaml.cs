using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.MacPlatform;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.AvaloniaUI.Windows;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI;

/// <summary>
/// Ported from HexManiac.WPF/Windows/App.xaml.cs.
///
/// Differences that are platform, not preference:
/// * WPF's Main() shows a `SplashScreen` before the Application exists. There is no workable
///   equivalent on this platform - see the Splash region below for what was tried and why.
/// * The single-instance handshake keeps WPF's design (a named mutex plus a named pipe), but the
///   identifier has to survive being used as a file name on Unix: .NET maps both to files under
///   the temp directory, and a '/' in the name throws. Every separator becomes '_'.
/// * WPF calls `MainWindow.Activate()` and un-minimises through WindowState; Avalonia spells the
///   latter WindowState.Normal on the same property, so that part transfers directly.
/// </summary>
public partial class App : Application {
   public const string ReleaseUrl = "https://github.com/haven1433/HexManiacAdvance/releases";
   public const string
      Arg_Skip_Splash_Screen = "--skip-splash",
      Arg_No_Metadata = "--no-metadata",
      Arg_Developer_Menu = "--dev-menu",
      // Not in HexManiac.WPF: brings the editor up with the MCP server already listening, so an
      // assistant can attach without the user having to click through a menu first.
      Arg_Mcp_Server = "--mcp";

   private readonly string appInstanceIdentifier;
   private MainWindow mainWindow;

   public App() {
      // WPF names the pipe "{HexManiacAdvance} : <assembly location>", so a debug and a release
      // build (or 0.3 and 0.4) can run side by side. That exact string cannot be used here: a
      // named pipe on Unix is a socket at /tmp/CoreFxPipe_<name>, and sockaddr_un.sun_path caps
      // the whole path at 104 bytes, which this one blows past. Hash the location instead - same
      // "one instance per binary" behaviour, a name that fits.
      var location = typeof(App).Assembly.Location;
      var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(location));
      appInstanceIdentifier = "HexManiacAdvance-" + Convert.ToHexString(hash, 0, 8);
   }

   public override void Initialize() => AvaloniaXamlLoader.Load(this);

   public override void OnFrameworkInitializationCompleted() {
      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
         var args = desktop.Args is { Length: > 0 } fromLifetime ? fromLifetime : ProcessArguments();
         var (path, address, options) = ParseArgs(args);

         if (!Arg_Skip_Splash_Screen.IsAny(args)) ShowSplash();

         if (TryForwardToExistingInstance(path, address)) {
            // Another copy of this binary is already up: it has the file now, so quit. This runs
            // before the main loop starts, so desktop.Shutdown() has nothing to unwind and the
            // process would sit there windowless - exit outright, which is what WPF's Shutdown()
            // achieved from the same place.
            Environment.Exit(0);
            return;
         }

         mainWindow = new MainWindow();
         var fileSystem = new MacFileSystem(() => mainWindow);
         var viewModel = GetViewModel(path, address, fileSystem, options);
         SetupServer(viewModel, fileSystem);

         DebugLog(viewModel, args);
         DebugLog(viewModel, "Have Editor");

         // HexManiac.WPF/Windows/App.xaml.cs does exactly this after building the view model:
         // fill the theme dictionary, then refresh it whenever the user edits the theme.
         UpdateThemeDictionary(Resources, viewModel.Theme);
         viewModel.Theme.PropertyChanged += (sender, _) => UpdateThemeDictionary(Resources, viewModel.Theme);

         // WPF exposes these as window resources so XAML can reach them with {DynamicResource}:
         // FileSystem as a CommandParameter, and the shared palette mixer used by ImageEditorView.
         mainWindow.Resources["FileSystem"] = fileSystem;
         mainWindow.Resources["PaletteMixer"] = new PaletteCollection().Fluent(mixer => mixer.SetContents(new short[16]));
         mainWindow.Resources["IsPaletteMixerExpanded"] = new EditableValue<bool>();

         mainWindow.FileSystem = fileSystem;
         mainWindow.Editor = viewModel; // sets DataContext and builds the key bindings

         // WPF hooked Application.Current.DispatcherUnhandledException and replaced the Trace
         // listeners once the window loaded. Avalonia splits the first of those in two: exceptions
         // raised while the dispatcher is pumping come through Dispatcher.UIThread.UnhandledException
         // (which must be marked Handled to keep the app alive, exactly like WPF's event args),
         // and anything on another thread only ever reaches AppDomain.UnhandledException.
         Dispatcher.UIThread.UnhandledException += (sender, e) => {
            CrashReporter.Report(viewModel, fileSystem, mainWindow.Title, e.Exception);
            e.Handled = true;
         };
         AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
            if (e.ExceptionObject is Exception exception) {
               CrashReporter.Report(viewModel, fileSystem, mainWindow.Title, exception);
            }
         };
         System.Diagnostics.Trace.Listeners.Clear();
         System.Diagnostics.Trace.Listeners.Add(new CustomTraceListener(fileSystem, viewModel));
         desktop.MainWindow = mainWindow;
         mainWindow.Opened += (sender, e) => CloseSplash();
         mainWindow.Show();
         if (args.Any(argument => argument == Arg_Mcp_Server)) mainWindow.StartMcpServer(announce: false);
         DebugLog(viewModel, "All Started!");

         WatchForFilesOpenedByTheSystem(viewModel, fileSystem);
      }
      base.OnFrameworkInitializationCompleted();
   }

   #region Splash

   /// <summary>
   /// WPF's Main() shows a `System.Windows.SplashScreen` - a Win32 layered window drawn by the
   /// loader before the CLR UI starts - and closes it once MainWindow appears.
   ///
   /// There is no equivalent here, and the obvious substitute does not work: a window created
   /// during OnFrameworkInitializationCompleted is built before Avalonia's macOS main loop is
   /// running, and the native window it creates then outlives Close() - the managed Window goes
   /// away while an empty 300x180 frame stays on screen for the life of the process. Deferring
   /// the close (Opened, Hide-then-Close, a posted Background close) does not change that.
   ///
   /// A ghost window is worse than no splash, so there is none. `--skip-splash` is still accepted
   /// and still parsed, so command lines written for the Windows build transfer unchanged; macOS
   /// shows its own launch feedback (the bouncing Dock icon) in the meantime.
   /// </summary>
   private void ShowSplash() { }

   private void CloseSplash() { }

   #endregion

   #region Single instance

   private void SetupServer(EditorViewModel viewModel, MacFileSystem fileSystem) {
      Task.Factory.StartNew(() => {
         while (true) {
            try {
               using var singleInstanceServer = new NamedPipeServerStream(appInstanceIdentifier);
               singleInstanceServer.WaitForConnection();
               using var reader = new StreamReader(singleInstanceServer);
               var line = reader.ReadLine();
               Dispatcher.UIThread.Post(() => {
                  AcceptParams(viewModel, fileSystem, line);
                  mainWindow?.Activate();
                  if (mainWindow != null && mainWindow.WindowState == WindowState.Minimized) {
                     mainWindow.WindowState = WindowState.Normal;
                  }
               });
            } catch {
               // A failed handshake must not take down the app. Pause before retrying so a
               // permanent failure (a name the platform rejects, say) cannot spin the CPU.
               Thread.Sleep(1000);
            }
         }
      }, TaskCreationOptions.LongRunning);
   }

   /// <summary>
   /// WPF gates single-instance on a named Mutex and only then opens the pipe. That does not
   /// transfer: on macOS the two processes here (one launched by launchd from the .app bundle,
   /// one from a shell) both reported createdNew == true for the same mutex name, so the mutex
   /// never detected the running copy. The pipe alone is enough and is what actually carries the
   /// message anyway - if a connect succeeds, someone is listening, so hand over and exit.
   /// </summary>
   /// <summary>
   /// Double-clicking a .gba in Finder, dropping one on the app's icon, or "Open With" does not put
   /// the file in argv. macOS sends it to the running process as an open-documents event, which
   /// Avalonia surfaces as <see cref="FileActivatedEventArgs"/> - and the bundle's Info.plist
   /// advertises .gba as a document type, so ignoring this means the app opens to an empty editor
   /// after promising it could handle the file.
   ///
   /// The event can arrive before or after the window is up, which is why the handler goes through
   /// the dispatcher rather than touching the editor directly.
   /// </summary>
   private void WatchForFilesOpenedByTheSystem(EditorViewModel editor, MacFileSystem fileSystem) {
      // Not `ApplicationLifetime as IActivatableLifetime`: the desktop lifetime does not implement
      // it, and that cast fails silently - Avalonia's own docs on the interface say to come through
      // TryGetFeature instead.
      if (TryGetFeature(typeof(IActivatableLifetime)) is not IActivatableLifetime activatable) return;
      activatable.Activated += (sender, e) => {
         if (e is not FileActivatedEventArgs fileActivated) return;
         foreach (var file in fileActivated.Files) {
            var path = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            Dispatcher.UIThread.Post(() => TryOpenFile(editor, fileSystem, path, -1));
         }
      };
   }

   private bool TryForwardToExistingInstance(string path, int address) {
      if (path != string.Empty && File.Exists(path)) path = Path.GetFullPath(path);
      try {
         using var client = new NamedPipeClientStream(appInstanceIdentifier);
         client.Connect(500);
         using var writer = new StreamWriter(client);
         writer.WriteLine($"{path}(){address}");
         writer.Flush();
         return true;
      } catch {
         // Nobody listening: this process is the first instance.
         return false;
      }
   }

   private void AcceptParams(EditorViewModel editor, MacFileSystem fileSystem, string line) {
      if (string.IsNullOrEmpty(line)) return;
      var parts = line.Split("()");
      if (parts.Length < 2 || !File.Exists(parts[0])) return;
      int.TryParse(parts[1], out int address);
      TryOpenFile(editor, fileSystem, parts[0], address);
   }

   #endregion

   #region Startup arguments

   /// <summary>
   /// The arguments this process was actually started with.
   ///
   /// On macOS `desktop.Args` comes back empty for `HexManiacAdvance somefile.gba`: the backend
   /// expects documents to arrive the Mac way, through application:openFiles:, and a bare
   /// executable that declares no document types never gets them - LaunchServices logs
   /// kLSApplicationNotFoundErr and drops the file. argv still has it, so fall back to that and
   /// `HexManiacAdvance rom.gba` behaves the way it does on Windows.
   ///
   /// -psn_0_123456 is the process serial number Finder passes to bundled apps; it is not a file.
   /// </summary>
   private static string[] ProcessArguments() {
      var all = Environment.GetCommandLineArgs();
      if (all.Length < 2) return Array.Empty<string>();
      return all.Skip(1).Where(arg => !arg.StartsWith("-psn_")).ToArray();
   }

   private static (string path, int address, bool[] options) ParseArgs(string[] args) {
      var useMetadata = !args.Any(arg => arg == Arg_No_Metadata);
      var showDevMenu = args.Any(arg => arg == Arg_Developer_Menu);
      args = args.Where(arg => !arg.StartsWith("--")).ToArray();

      var allArgs = args.Aggregate(string.Empty, (a, b) => a + ' ' + b).Trim();
      var loadAddress = -1;
      if (allArgs.Contains(":") && allArgs.LastIndexOf(":") > 4) {
         var parts = allArgs.Split(':');
         if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
         allArgs = parts.Take(parts.Length - 1).Aggregate((a, b) => a + ":" + b).Trim();
      } else if (allArgs.ToLower().Contains(".gba ")) {
         var parts = allArgs.Split(" ");
         if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
         allArgs = parts.Take(parts.Length - 1).Aggregate(string.Empty, (a, b) => a + " " + b).Trim();
      }

      return (allArgs, loadAddress, new[] { useMetadata, showDevMenu });
   }

   /// <summary>
   /// Generally, the initial working directory is set to wherever the program was launched from.
   /// In the case of command line usage or dropping a file onto the app, that's not the app's
   /// location. We want the initial loading directory to match so we can find the reference files
   /// (resources/armReference.txt, resources/scriptReference.txt, ...).
   /// </summary>
   private static void SetInitialWorkingDirectory() {
      var mainAssemblyLocation = Assembly.GetExecutingAssembly().Location;
      var workingDirectory = Path.GetDirectoryName(mainAssemblyLocation);
      if (!string.IsNullOrEmpty(workingDirectory)) Directory.SetCurrentDirectory(workingDirectory);
   }

   private static EditorViewModel GetViewModel(string fileName, int address, MacFileSystem fileSystem, bool[] options) {
      bool useMetadata = options[0];
      bool showDevMenu = options[1];
      if (fileName != string.Empty) fileName = Path.GetFullPath(fileName);
      SetInitialWorkingDirectory();
      var editor = new EditorViewModel(fileSystem, fileSystem, allowLoadingMetadata: useMetadata) { ShowDeveloperMenu = showDevMenu };
      DebugLog(editor, "------");
      DebugLog(editor, fileName);
      CheckIsNewerVersionAvailable(editor);
      if (!File.Exists(fileName)) return editor;
      DebugLog(editor, "File Exists");
      TryOpenFile(editor, fileSystem, fileName, address);
      return editor;
   }

   private static void TryOpenFile(EditorViewModel editor, MacFileSystem fileSystem, string fileName, int address) {
      var loadedFile = fileSystem.LoadFile(fileName);
      if (loadedFile == null) return;
      editor.Open.Execute(loadedFile);
      DebugLog(editor, "Tab Added");
      if (editor[editor.SelectedIndex] is ViewPort tab && address >= 0) {
         DebugLog(editor, $"Loading at Script {address:X6}.");
         tab.Model.InitializationWorkload.ContinueWith(
            task => fileSystem.DispatchWork(() => tab.CascadeScript(address)),
            TaskContinuationOptions.ExecuteSynchronously);
         editor.GotoViewModel.ControlVisible = false;
      }
   }

   [Conditional("DEBUG")]
   private static void DebugLog(EditorViewModel editor, params string[] text) {
      if (editor.LogAppStartupProgress) {
         File.AppendAllLines("HexManiacAdvance.debug.txt", text);
      }
   }

   private static void CheckIsNewerVersionAvailable(EditorViewModel viewModel) {
      if (DateTime.Now < viewModel.LastUpdateCheck + TimeSpan.FromDays(1)) return;
      viewModel.LastUpdateCheck = DateTime.Now;
      try {
         using var client = new HttpClient();
         string content = client.GetStringAsync(ReleaseUrl).Result;
         var mostRecentVersion = content
            .Split('\n')
            .Where(line => line.Contains("/haven1433/HexManiacAdvance/tree/"))
            .Select(line => line.Split("title=").Last().Split('"')[1].Split('v').Last())
            .First();
         viewModel.IsNewVersionAvailable = StoredMetadata.NeedVersionUpdate(viewModel.Singletons.MetadataInfo.VersionNumber, mostRecentVersion);
      } catch {
         // If anything goes wrong we probably don't care: IsNewVersionAvailable just stays false.
      }
   }

   #endregion

   /// <summary>Port of HexManiac.WPF/Windows/App.xaml.cs UpdateThemeDictionary.</summary>
   public static void UpdateThemeDictionary(IResourceDictionary resources, Theme theme) =>
      ThemeDictionary.Fill(resources, theme);
}
