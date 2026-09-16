using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Emulation;
using HavenSoft.HexManiac.AvaloniaUI.Project;
using HavenSoft.HexManiac.AvaloniaUI.Windows;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Mcp;

/// <summary>
/// The tools the MCP server offers, implemented against the editor that is actually running.
///
/// Everything here hops to the UI thread before touching a view model: HexManiac.Core is built on
/// the assumption of a single thread, and an MCP request arrives on a listener thread.
///
/// Two deliberate choices about how much rope to hand out:
/// * Writes go through the editor's change history, so the user can undo anything an assistant
///   did, and nothing is ever written to the .gba - saving stays the user's decision.
/// * The structured tools (table_read/table_write) are described as preferable to raw bytes,
///   because they go through the model and keep pointers, string encoding and table metadata
///   correct. run_python is the escape hatch for everything they do not cover.
/// </summary>
public class HexManiacTools {
   private readonly Func<MainWindow> getWindow;

   public HexManiacTools(Func<MainWindow> getWindow) => this.getWindow = getWindow;

   public IReadOnlyList<McpTool> BuildTools() => new[] {
      Tool("rom_status", "ROM status",
         "What the editor currently has open: each tab, the selected one, the game code, ROM size, " +
         "free space, and whether there are unsaved changes. Call this first - every other tool acts " +
         "on the selected tab.",
         Schema(),
         _ => OnUi(RomStatus)),

      Tool("rom_goto", "Go to address",
         "Moves the editor's view to an address or anchor name, exactly as typing it into the goto box " +
         "would. Use it to show the user what you are working on; the view follows you. " +
         "Accepts a hex address (\"0x123456\" or \"123456\") or an anchor (\"data.pokemon.stats\").",
         Schema(("destination", "string", "Hex address or anchor name.", true)),
         args => OnUi(() => Goto(Text(args, "destination")))),

      Tool("rom_read_bytes", "Read bytes",
         "Reads raw bytes as hex. Prefer table_read when the data is a known table - this is for " +
         "poking at unformatted regions.",
         Schema(("address", "string", "Hex address or anchor name.", true),
                ("length", "integer", "How many bytes to read (default 64, max 4096).", false)),
         args => OnUi(() => ReadBytes(Text(args, "address"), Int(args, "length", 64)))),

      Tool("rom_write_bytes", "Write bytes",
         "Writes raw bytes at an address. Goes into the editor's undo history and is NOT saved to disk. " +
         "This bypasses the model's understanding of the data, so prefer table_write or run_python " +
         "unless you specifically mean to patch bytes.",
         Schema(("address", "string", "Hex address or anchor name.", true),
                ("bytes", "string", "Hex byte string, e.g. \"00 1F A2\" or \"001FA2\".", true)),
         args => OnUi(() => WriteBytes(Text(args, "address"), Text(args, "bytes")))),

      Tool("rom_search", "Search the ROM",
         "Searches the open ROM the same way the editor's find box does: hex bytes (\"1F 00 A2\"), " +
         "text in quotes (\"\\\"POKEMON\\\"\"), or an anchor name. Returns the matches it found.",
         Schema(("query", "string", "What to search for.", true),
                ("limit", "integer", "Maximum matches to return (default 25).", false)),
         args => OnUi(() => Search(Text(args, "query"), Int(args, "limit", 25)))),

      Tool("table_read", "Read table rows",
         "Reads rows from a named table through the editor's model, so every field comes back with its " +
         "real name and value (numbers, text, enum names and pointers all resolved). " +
         "Example: table \"data.pokemon.stats\", index 1 gives Bulbasaur's stats.",
         Schema(("table", "string", "Table anchor name, e.g. data.pokemon.stats.", true),
                ("index", "integer", "First row to read (default 0).", false),
                ("count", "integer", "How many rows (default 1, max 50).", false)),
         args => OnUi(() => TableRead(Text(args, "table"), Int(args, "index", 0), Int(args, "count", 1)))),

      Tool("table_write", "Write a table field",
         "Sets one field of one row of a named table, through the model - so text is encoded correctly " +
         "and numeric fields keep their width. Goes into the undo history; nothing is written to disk.",
         Schema(("table", "string", "Table anchor name.", true),
                ("index", "integer", "Row to change.", true),
                ("field", "string", "Field name, as reported by table_read.", true),
                ("value", "string", "New value. A number for numeric fields, text for text fields.", true)),
         args => OnUi(() => TableWrite(Text(args, "table"), Int(args, "index", 0), Text(args, "field"), Text(args, "value")))),

      Tool("list_tables", "List tables",
         "Lists the tables the editor knows about in this ROM, with their element counts - " +
         "the fastest way to find the right name for table_read.",
         Schema(("filter", "string", "Only return names containing this text.", false),
                ("limit", "integer", "Maximum names to return (default 60).", false)),
         args => OnUi(() => ListTables(Text(args, "filter"), Int(args, "limit", 60)))),

      Tool("rom_undo", "Undo",
         "Undoes the most recent change in the editor - including your own. Use it to back out an " +
         "edit that did not work rather than trying to reverse it by hand.",
         Schema(("times", "integer", "How many changes to undo (default 1).", false)),
         args => OnUi(() => UndoRedo(undo: true, Int(args, "times", 1)))),

      Tool("rom_redo", "Redo",
         "Redoes changes that were undone.",
         Schema(("times", "integer", "How many changes to redo (default 1).", false)),
         args => OnUi(() => UndoRedo(undo: false, Int(args, "times", 1)))),

      Tool("project_export", "Export the ROM's tables as text",
         "Writes every table to a tab-separated file under the given folder, plus a manifest, so a " +
         "hack can be tracked in version control and changes can be diffed and reviewed. Pointers " +
         "are written for context but are not read back.",
         Schema(("directory", "string", "Folder to write into. It is created if missing.", true)),
         args => OnUi(() => ProjectExportTool(Text(args, "directory")))),

      Tool("project_import", "Apply an exported project back into the ROM",
         "Reads a folder produced by project_export and applies the values to the open ROM as one " +
         "undoable change. Refuses a ROM whose game code does not match the export. Nothing is " +
         "written to the .gba file.",
         Schema(("directory", "string", "The exported project folder.", true)),
         args => OnUi(() => ProjectImportTool(Text(args, "directory")))),

      Tool("run_python", "Run an automation script",
         "Runs a script in HexManiacAdvance's own Python automation engine - the same one behind the " +
         "editor's Python panel, with the same API. Tables are in scope by name: " +
         "`data.pokemon.stats[1].hp = 100`. The value of the last expression is returned, so end with " +
         "an expression rather than calling print(). This is the escape hatch for anything the other " +
         "tools do not cover; changes land in the undo history.",
         Schema(("code", "string", "Python source to execute.", true)),
         args => OnUi(() => RunPython(Text(args, "code")))),

      Tool("emulator_boot", "Boot the ROM in the emulator",
         "Opens (or reuses) the emulator window and boots the ROM as it stands right now, including " +
         "your unsaved edits. Use this after making a change to see whether it actually works in game.",
         Schema(),
         _ => OnUi(EmulatorBoot)),

      Tool("emulator_screenshot", "See the emulator screen",
         "Returns the emulator's current frame as an image, so you can look at the running game. " +
         "The emulator must already be running - call emulator_boot first.",
         Schema(),
         _ => OnUi(EmulatorScreenshot)),

      Tool("emulator_memory", "Read the running game's memory",
         "Reads live RAM out of the running game - what the ROM has actually done, as opposed to " +
         "what it contains. Use it to check whether an edit had the effect you expected: is the flag " +
         "set, did the variable change, is the party what you wrote. " +
         "Call with no arguments to list the regions this core exposes and their GBA addresses. " +
         "Note which core is running: mGBA publishes only 32KB of IWRAM, while VBA-M, VBA-Next and " +
         "gpSP publish the 256KB of EWRAM at 0x02000000 where a Pokemon game keeps flags, variables " +
         "and the party.",
         Schema(("address", "string", "GBA address to read, e.g. \"0x02024EA4\". Omit to list the regions.", false),
                ("length", "integer", "How many bytes (default 64, max 1024).", false)),
         args => OnUi(() => EmulatorMemory(Text(args, "address"), Int(args, "length", 64)))),

      Tool("emulator_input", "Press buttons in the emulator",
         "Holds one or more GBA buttons for a number of frames, then releases them, letting the game " +
         "run. Use it to walk to the thing you changed and look at it. " +
         "Buttons: a, b, l, r, start, select, up, down, left, right.",
         Schema(("buttons", "string", "Comma-separated buttons to hold, e.g. \"a\" or \"up,b\".", true),
                ("frames", "integer", "Frames to hold them (default 6; 60 frames is one second).", false),
                ("then_wait_frames", "integer", "Frames to let the game run after releasing (default 30).", false)),
         args => EmulatorInput(Text(args, "buttons"), Int(args, "frames", 6), Int(args, "then_wait_frames", 30))),
   };

   #region Tool implementations (all on the UI thread)

   private McpToolResult RomStatus() {
      var editor = Editor;
      if (editor == null) return McpToolResult.Error("The editor is not ready yet.");
      var text = new StringBuilder();
      text.AppendLine($"Tabs open: {editor.Count}, selected: {editor.SelectedIndex}");
      for (int i = 0; i < editor.Count; i++) {
         var tab = editor[i];
         var marker = i == editor.SelectedIndex ? "* " : "  ";
         text.Append($"{marker}[{i}] {tab.Name}");
         if (ViewPortFor(tab) is IEditableViewPort viewPort) {
            text.Append($"  game={viewPort.Model.GetGameCode()} size=0x{viewPort.Model.Count:X}");
            text.Append(viewPort.ChangeHistory.HasDataChange ? "  (unsaved changes)" : "  (no unsaved changes)");
         }
         text.AppendLine();
      }
      if (SelectedViewPort is IEditableViewPort selected) {
         text.AppendLine($"Free space starts near 0x{selected.Model.FreeSpaceStart:X6}");
      }
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private McpToolResult Goto(string destination) {
      var tab = Editor?.SelectedTab;
      if (tab == null) return McpToolResult.Error("No tab is open.");
      if (!tab.Goto.CanExecute(destination)) return McpToolResult.Error($"Cannot go to '{destination}'.");
      tab.Goto.Execute(destination);
      return new McpToolResult($"Editor moved to {destination}.");
   }

   private McpToolResult ReadBytes(string address, int length) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      length = Math.Clamp(length, 1, 4096);
      var start = ResolveAddress(viewPort.Model, address);
      if (start < 0) return McpToolResult.Error($"Could not resolve '{address}' to an address.");
      if (start + length > viewPort.Model.Count) length = viewPort.Model.Count - start;

      var text = new StringBuilder();
      text.AppendLine($"0x{start:X6}, {length} bytes:");
      for (int offset = 0; offset < length; offset += 16) {
         text.Append($"{start + offset:X6}  ");
         for (int i = 0; i < 16 && offset + i < length; i++) text.Append($"{viewPort.Model[start + offset + i]:X2} ");
         text.AppendLine();
      }
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private McpToolResult WriteBytes(string address, string hex) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      var start = ResolveAddress(viewPort.Model, address);
      if (start < 0) return McpToolResult.Error($"Could not resolve '{address}' to an address.");

      var bytes = ParseHex(hex);
      if (bytes == null) return McpToolResult.Error($"'{hex}' is not a hex byte string.");
      if (start + bytes.Length > viewPort.Model.Count) return McpToolResult.Error("That write would run past the end of the ROM.");

      var token = viewPort.ChangeHistory.CurrentChange;
      for (int i = 0; i < bytes.Length; i++) viewPort.Model.WriteValue(token, start + i, bytes[i]);
      CompleteChange(viewPort);
      return new McpToolResult($"Wrote {bytes.Length} bytes at 0x{start:X6}. The change is in the editor's undo history and has not been saved to disk.");
   }

   private McpToolResult Search(string query, int limit) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      limit = Math.Clamp(limit, 1, 200);
      var results = viewPort.Find(query);
      if (results.Count == 0) return new McpToolResult($"No matches for {query}.");
      var text = new StringBuilder($"{results.Count} match(es) for {query}:");
      text.AppendLine();
      foreach (var (start, end) in results.Take(limit)) {
         text.AppendLine($"  0x{start:X6} - 0x{end:X6}  {viewPort.Model.GetAnchorFromAddress(-1, start) ?? string.Empty}");
      }
      if (results.Count > limit) text.AppendLine($"  ... {results.Count - limit} more");
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private McpToolResult ListTables(string filter, int limit) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      limit = Math.Clamp(limit, 1, 400);

      var names = viewPort.Model.Arrays
         .Select(run => (Run: run, Name: viewPort.Model.GetAnchorFromAddress(-1, run.Start)))
         .Where(entry => !string.IsNullOrEmpty(entry.Name))
         .Where(entry => string.IsNullOrEmpty(filter) || entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
         .OrderBy(entry => entry.Name)
         .ToList();

      if (names.Count == 0) return new McpToolResult(string.IsNullOrEmpty(filter) ? "No tables found." : $"No table name contains '{filter}'.");
      var text = new StringBuilder($"{names.Count} table(s)" + (string.IsNullOrEmpty(filter) ? string.Empty : $" matching '{filter}'") + ":");
      text.AppendLine();
      foreach (var entry in names.Take(limit)) {
         text.AppendLine($"  {entry.Name}  ({entry.Run.ElementCount} entries at 0x{entry.Run.Start:X6})");
      }
      if (names.Count > limit) text.AppendLine($"  ... {names.Count - limit} more");
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private McpToolResult TableRead(string tableName, int index, int count) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      count = Math.Clamp(count, 1, 50);

      var table = viewPort.Model.GetTableModel(tableName, () => viewPort.ChangeHistory.CurrentChange);
      if (table == null) return McpToolResult.Error($"No table named '{tableName}'. Try list_tables.");
      if (index < 0 || index >= table.Count) return McpToolResult.Error($"'{tableName}' has {table.Count} entries; {index} is out of range.");

      var text = new StringBuilder($"{tableName}: {table.Count} entries");
      text.AppendLine();
      for (int i = index; i < Math.Min(index + count, table.Count); i++) {
         var element = table[i];
         text.AppendLine($"[{i}] at 0x{element.Start:X6}");
         foreach (var segment in element.Table.ElementContent) {
            text.AppendLine($"    {segment.Name} = {DescribeField(element, segment)}");
         }
      }
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private static string DescribeField(ModelArrayElement element, ArrayRunElementSegment segment) {
      try {
         return segment.Type switch {
            ElementContentType.PCS => "\"" + element.GetStringValue(segment.Name) + "\"",
            ElementContentType.Pointer => "<" + element.GetStringValue(segment.Name) + ">",
            _ => element.GetValue(segment.Name).ToString(),
         };
      } catch (Exception e) {
         return $"(unreadable: {e.Message})";
      }
   }

   private McpToolResult TableWrite(string tableName, int index, string field, string value) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;

      var table = viewPort.Model.GetTableModel(tableName, () => viewPort.ChangeHistory.CurrentChange);
      if (table == null) return McpToolResult.Error($"No table named '{tableName}'. Try list_tables.");
      if (index < 0 || index >= table.Count) return McpToolResult.Error($"'{tableName}' has {table.Count} entries; {index} is out of range.");

      var element = table[index];
      if (!element.HasField(field)) {
         var available = string.Join(", ", element.Table.ElementContent.Select(segment => segment.Name));
         return McpToolResult.Error($"'{tableName}' has no field '{field}'. Fields: {available}");
      }

      var segment = element.Table.ElementContent.Single(s => s.Name == field);
      var before = DescribeField(element, segment);
      if (segment.Type == ElementContentType.PCS) {
         element.SetStringValue(field, value);
      } else if (int.TryParse(value, out var number)) {
         element.SetValue(field, number);
      } else if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number)) {
         element.SetValue(field, number);
      } else {
         return McpToolResult.Error($"'{field}' is numeric, but '{value}' is not a number.");
      }

      CompleteChange(viewPort);
      var after = DescribeField(table[index], segment);
      return new McpToolResult($"{tableName}[{index}].{field}: {before} -> {after}. In the undo history, not saved to disk.");
   }

   /// <summary>
   /// Seals what a tool just wrote into its own undo step.
   ///
   /// HexManiac accumulates edits in ChangeHistory.CurrentChange and only pushes them onto the
   /// undo stack when something calls ChangeCompleted - which the editor does at the end of a user
   /// gesture. A tool call is the equivalent gesture here, so without this an assistant's edits
   /// stay in an uncommitted token that Undo cannot reach, and the user cannot back them out.
   /// </summary>
   private static void CompleteChange(IEditableViewPort viewPort) {
      viewPort.ChangeHistory.ChangeCompleted();
      viewPort.Refresh();
   }

   /// <summary>
   /// Undo/redo, driven through the selected tab rather than through EditorViewModel.
   ///
   /// EditorViewModel wraps these two with preventIfScreenBlocked, so they do nothing at all while
   /// the goto panel is covering the editor - and since that panel is what the app opens on, an
   /// assistant's undo would silently no-op, the more confusingly because the wrapper's CanExecute
   /// does not check the same condition and still says yes. The tab's own command has no such
   /// guard, which is the right behaviour here: an MCP client is not looking at the screen.
   /// </summary>
   private McpToolResult ProjectExportTool(string directory) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      if (string.IsNullOrWhiteSpace(directory)) return McpToolResult.Error("Give a folder to export into.");
      var result = ProjectExport.Export(viewPort.Model, directory);
      return new McpToolResult($"Exported {result.Tables:N0} tables, {result.Rows:N0} rows, {result.Fields:N0} values to {directory}.");
   }

   private McpToolResult ProjectImportTool(string directory) {
      var viewPort = RequireViewPort(out var error);
      if (error != null) return error;
      if (string.IsNullOrWhiteSpace(directory)) return McpToolResult.Error("Give an exported project folder.");
      var result = ProjectExport.Import(viewPort, directory);
      var notes = result.Notes.Count == 0 ? string.Empty
         : Environment.NewLine + "Skipped:" + Environment.NewLine + string.Join(Environment.NewLine, result.Notes.Take(20));
      return new McpToolResult(
         $"Applied {result.Fields:N0} value(s) across {result.Tables:N0} table(s). " +
         "One entry in the undo history; nothing written to disk." + notes);
   }

   private McpToolResult UndoRedo(bool undo, int times) {
      var tab = Editor?.SelectedTab;
      if (tab == null) return McpToolResult.Error("No tab is open.");
      times = Math.Clamp(times, 1, 100);
      var command = undo ? tab.Undo : tab.Redo;
      if (command == null) return McpToolResult.Error("This tab does not support undo.");

      var applied = 0;
      for (int i = 0; i < times && command.CanExecute(null); i++) {
         command.Execute(null);
         applied++;
      }
      tab.Refresh();
      if (applied == 0) return new McpToolResult(undo ? "Nothing left to undo." : "Nothing left to redo.");
      return new McpToolResult($"{(undo ? "Undid" : "Redid")} {applied} change(s).");
   }

   private McpToolResult RunPython(string code) {
      var editor = Editor;
      if (editor?.PythonTool == null) return McpToolResult.Error("The Python automation engine is not available.");

      // The engine's built-in print() opens a modal message box, which would hang this call and
      // trap the user behind a dialog they did not ask for. Swap in a collector for the duration.
      var output = new StringBuilder();
      editor.PythonTool.AddVariable("print", (Action<object>)(value => output.AppendLine(value?.ToString() ?? "None")));

      var result = editor.PythonTool.RunPythonScript(code);
      if (SelectedViewPort is IEditableViewPort viewPort) CompleteChange(viewPort);
      else editor.SelectedTab?.Refresh();

      var text = new StringBuilder();
      if (output.Length > 0) text.AppendLine(output.ToString().TrimEnd());
      if (result.HasError && !result.IsWarning) {
         text.AppendLine(result.ErrorMessage);
         return new McpToolResult(text.ToString().TrimEnd(), IsError: true);
      }
      if (result.IsWarning && !string.IsNullOrEmpty(result.ErrorMessage)) text.AppendLine(result.ErrorMessage);
      if (text.Length == 0) text.Append("(no output)");
      return new McpToolResult(text.ToString().TrimEnd());
   }

   private McpToolResult EmulatorBoot() {
      var window = getWindow();
      if (window == null) return McpToolResult.Error("The editor window is not ready yet.");
      if (RequireViewPort(out var error) == null) return error;
      var alreadyOpen = window.IsEmulatorOpen;
      window.OpenEmulator();
      if (alreadyOpen) window.ReloadEmulatorRom();
      return new McpToolResult("Booting the ROM as it currently stands in the editor. Give it a few seconds, then call emulator_screenshot.");
   }

   private McpToolResult EmulatorMemory(string address, int length) {
      var window = getWindow();
      var regions = window?.EmulatorMemoryRegions ?? Array.Empty<MemoryRegion>();
      if (regions.Count == 0) return McpToolResult.Error("The emulator is not running. Call emulator_boot first.");

      if (string.IsNullOrWhiteSpace(address)) {
         var list = new StringBuilder("Live memory this core exposes:");
         list.AppendLine();
         foreach (var region in regions) {
            list.AppendLine($"  {region.Name,-9} 0x{region.GbaAddress:X8} - 0x{region.GbaAddress + region.Size - 1:X8}  ({region.Size:N0} bytes)");
         }
         return new McpToolResult(list.ToString().TrimEnd());
      }

      var text = address.Trim();
      if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
      if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gbaAddress)) {
         return McpToolResult.Error($"'{address}' is not a hex address.");
      }

      var match = regions.FirstOrDefault(r => gbaAddress >= r.GbaAddress && gbaAddress < r.GbaAddress + r.Size);
      if (match == null) {
         var available = string.Join(", ", regions.Select(r => $"{r.Name} 0x{r.GbaAddress:X8}+{r.Size:X}"));
         return McpToolResult.Error($"0x{gbaAddress:X8} is not in any region this core exposes. Available: {available}");
      }

      length = Math.Clamp(length, 1, 1024);
      var bytes = window.ReadEmulatorMemory(match.Id, (int)(gbaAddress - match.GbaAddress), length);
      if (bytes == null) return McpToolResult.Error("That region could not be read.");

      var dump = new StringBuilder($"{match.Name} at 0x{gbaAddress:X8}, {bytes.Length} bytes (live):");
      dump.AppendLine();
      for (int offset = 0; offset < bytes.Length; offset += 16) {
         dump.Append($"{gbaAddress + offset:X8}  ");
         for (int i = 0; i < 16 && offset + i < bytes.Length; i++) dump.Append($"{bytes[offset + i]:X2} ");
         dump.AppendLine();
      }
      return new McpToolResult(dump.ToString().TrimEnd());
   }

   private McpToolResult EmulatorScreenshot() {
      var window = getWindow();
      var png = window?.CaptureEmulatorFrame();
      if (png == null) return McpToolResult.Error("The emulator is not running. Call emulator_boot first.");
      return new McpToolResult("Current emulator frame.", Convert.ToBase64String(png), "image/png");
   }

   /// <summary>
   /// Holds buttons for a while, then releases them and lets the game run on.
   ///
   /// Asynchronous on purpose: the emulator runs in real time on its own thread, so "hold for 30
   /// frames" is half a second of wall clock. Sleeping the UI thread for that would freeze the
   /// editor, so this awaits between three short hops onto it.
   /// </summary>
   private async Task<McpToolResult> EmulatorInput(string buttons, int frames, int waitFrames) {
      frames = Math.Clamp(frames, 1, 600);
      waitFrames = Math.Clamp(waitFrames, 0, 600);
      var window = getWindow();
      if (window == null) return McpToolResult.Error("The editor window is not ready yet.");

      var pressed = await Dispatcher.UIThread.InvokeAsync(() => window.SetEmulatorButtons(buttons, true)).GetTask();
      if (pressed == null) return McpToolResult.Error("The emulator is not running. Call emulator_boot first.");
      if (pressed.Count == 0) {
         return McpToolResult.Error($"None of '{buttons}' is a GBA button. Use a, b, l, r, start, select, up, down, left, right.");
      }

      await Task.Delay(FramesToMilliseconds(frames));
      await Dispatcher.UIThread.InvokeAsync(() => window.SetEmulatorButtons(buttons, false)).GetTask();
      if (waitFrames > 0) await Task.Delay(FramesToMilliseconds(waitFrames));

      return new McpToolResult($"Held {string.Join(" + ", pressed)} for {frames} frames, then ran {waitFrames} more. Call emulator_screenshot to see the result.");
   }

   private static int FramesToMilliseconds(int frames) => (int)Math.Round(frames * 1000 / 59.7275);

   #endregion

   #region Plumbing

   private EditorViewModel Editor => getWindow()?.ViewModel;

   private IViewPort SelectedViewPort => ViewPortFor(Editor?.SelectedTab);

   private static IViewPort ViewPortFor(ITabContent tab) =>
      tab as IViewPort ?? (tab as MapEditorViewModel)?.ViewPort;

   private IEditableViewPort RequireViewPort(out McpToolResult error) {
      if (SelectedViewPort is IEditableViewPort viewPort) {
         error = null;
         return viewPort;
      }
      error = McpToolResult.Error("The selected tab is not an editable ROM view. Open a .gba file, or select its tab.");
      return null;
   }

   /// Accepts "0x123456", "123456", or an anchor name.
   private static int ResolveAddress(IDataModel model, string text) {
      if (string.IsNullOrWhiteSpace(text)) return -1;
      text = text.Trim();
      if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
      if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)) {
         return address >= 0 && address < model.Count ? address : -1;
      }
      var anchor = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, text.Trim());
      return anchor >= 0 && anchor < model.Count ? anchor : -1;
   }

   private static byte[] ParseHex(string text) {
      var cleaned = new string(text.Where(Uri.IsHexDigit).ToArray());
      if (cleaned.Length == 0 || cleaned.Length % 2 != 0) return null;
      var bytes = new byte[cleaned.Length / 2];
      for (int i = 0; i < bytes.Length; i++) {
         bytes[i] = byte.Parse(cleaned.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      }
      return bytes;
   }

   private static Task<McpToolResult> OnUi(Func<McpToolResult> work) =>
      Dispatcher.UIThread.InvokeAsync(work).GetTask();

   private static McpTool Tool(string name, string title, string description, JsonObject schema, Func<JsonObject, Task<McpToolResult>> invoke) =>
      new(name, title, description, schema, invoke);

   /// Builds a JSON Schema object for a tool's arguments.
   private static JsonObject Schema(params (string Name, string Type, string Description, bool Required)[] properties) {
      var propertyNodes = new JsonObject();
      var required = new JsonArray();
      foreach (var property in properties) {
         propertyNodes[property.Name] = new JsonObject {
            ["type"] = property.Type,
            ["description"] = property.Description,
         };
         if (property.Required) required.Add(property.Name);
      }
      var schema = new JsonObject {
         ["type"] = "object",
         ["properties"] = propertyNodes,
      };
      if (required.Count > 0) schema["required"] = required;
      return schema;
   }

   private static string Text(JsonObject args, string name) => args?[name]?.ToString() ?? string.Empty;

   private static int Int(JsonObject args, string name, int fallback) {
      var node = args?[name];
      if (node == null) return fallback;
      if (node.AsValue().TryGetValue<int>(out var value)) return value;
      return int.TryParse(node.ToString(), out value) ? value : fallback;
   }

   #endregion
}
