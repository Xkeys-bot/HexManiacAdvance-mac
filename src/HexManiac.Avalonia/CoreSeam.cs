using System.IO;
using System.Reflection;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using CorePoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.AvaloniaUI;

/// <summary>
/// THE SEAM: every call the Avalonia UI makes into HexManiac.Core goes through this file.
///
/// These calls were written from HMA's Developer Guide and architecture docs, not compiled
/// against the source, so some member names or signatures will be off. When the first build
/// fails, the errors should cluster here (and in MacFileSystem). Fix each one by looking at how
/// HexManiac.WPF does the same thing, e.g. HexContent.cs, MainWindow.xaml.cs, App.xaml.cs.
///
/// Keeping this in one place means the controls and views never need to change when a Core
/// API is corrected, and it doubles as a list of what the UI actually depends on.
/// </summary>
public static class CoreSeam {

   // ---------------- Editor (whole window) ----------------

   // Matches HexManiac.WPF/Windows/App.xaml.cs, which passes its file system as both the
   // IFileSystem and the IWorkDispatcher. Skipping the dispatcher leaves Core on
   // InstantDispatch, which runs ROM parsing inline on the UI thread.
   public static EditorViewModel CreateEditor(IFileSystem fileSystem) {
      SetInitialWorkingDirectory();
      return new EditorViewModel(fileSystem, fileSystem as IWorkDispatcher);
   }

   /// <summary>
   /// Core resolves its reference data with relative paths ("resources/armReference.txt"), so the
   /// working directory has to be the app directory before the first Singletons is built.
   /// HexManiac.WPF's App.xaml.cs does exactly this in SetInitialWorkingDirectory().
   /// </summary>
   static void SetInitialWorkingDirectory() {
      var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      if (!string.IsNullOrEmpty(appDirectory)) Directory.SetCurrentDirectory(appDirectory);
   }

   // Name confirmed by the architecture docs: MainWindow.OnClosing calls this.
   public static void SaveAppSettings(EditorViewModel editor) =>
      editor.WriteAppLevelMetadata();

   // Mirrors App.xaml.cs's TryOpenFile: read through the file system so a ROM held open by an
   // emulator still loads, and so read failures surface the same message as the WPF build.
   public static void OpenFile(EditorViewModel editor, IFileSystem fileSystem, string path) {
      var file = fileSystem.LoadFile(path);
      if (file == null) return;
      if (editor.Open.CanExecute(file)) editor.Open.Execute(file);
   }

   // ---------------- ViewPort (one hex tab) ----------------

   // VERIFY: ViewPort.Width / Height are the visible cell counts that HexContent sets on resize.
   public static int Columns(ViewPort viewPort) => viewPort.Width;
   public static int Rows(ViewPort viewPort) => viewPort.Height;

   public static void SetVisibleCellCount(ViewPort viewPort, int columns, int rows) {
      if (viewPort.Width != columns) viewPort.Width = columns;
      if (viewPort.Height != rows) viewPort.Height = rows;
   }

   // VERIFY: ViewPort indexer returns a HexElement with Value and Format.
   public static byte CellValue(ViewPort viewPort, int x, int y) => viewPort[x, y].Value;

   /// <summary>
   /// Text to draw in a cell. Phase 1 draws raw hex so the grid works end to end.
   /// Phase 2: route through Core's ConvertCellToText visitor (src/HexManiac.Core/ViewModels/ConvertCellToText.cs)
   /// and later a real FormatDrawer port, so pointers, text, enums, anchors etc. render properly.
   /// </summary>
   public static string CellText(ViewPort viewPort, int x, int y) =>
      CellValue(viewPort, x, y).ToString("X2");

   // VERIFY: selection API. Check how HexContent.cs handles mouse down / drag / shift-click.
   public static bool IsSelected(ViewPort viewPort, int x, int y) =>
      viewPort.IsSelected(new CorePoint(x, y));

   public static void StartSelection(ViewPort viewPort, int x, int y) =>
      viewPort.SelectionStart = new CorePoint(x, y);

   public static void ExtendSelection(ViewPort viewPort, int x, int y) =>
      viewPort.SelectionEnd = new CorePoint(x, y);

   public static void MoveCursor(ViewPort viewPort, int dx, int dy, bool extend) {
      if (extend) {
         var end = viewPort.SelectionEnd;
         viewPort.SelectionEnd = new CorePoint(end.X + dx, end.Y + dy);
      } else {
         var start = viewPort.SelectionStart;
         viewPort.SelectionStart = new CorePoint(start.X + dx, start.Y + dy);
      }
   }

   // VERIFY: scroll API (the WPF scrollbar binds to something like ScrollValue).
   public static void ScrollRows(ViewPort viewPort, int rows) =>
      viewPort.ScrollValue += rows;

   // VERIFY: typing into a cell. HexContent forwards text input to the ViewPort.
   public static void TypeText(ViewPort viewPort, string text) =>
      viewPort.Edit(text);
}
