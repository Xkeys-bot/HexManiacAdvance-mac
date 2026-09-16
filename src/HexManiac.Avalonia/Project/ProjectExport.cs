using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Project;

/// <summary>
/// Writes the ROM's tables out as text, and reads them back.
///
/// The point is version control. A .gba is one opaque 16MB blob: two people cannot work on a hack
/// at once, a change cannot be reviewed, and "what did I alter last week" has no answer. One
/// tab-separated file per table turns all of that into an ordinary diff.
///
/// Deliberately *not* a complete serialisation of the ROM - it is a data export. Pointers are
/// written for context but never imported, because rewriting one means relocating what it points
/// at, which is the editor's job and not a text file's. Import reports exactly what it skipped
/// rather than quietly doing half a job.
/// </summary>
public static class ProjectExport {
   private const string ManifestName = "manifest.txt";
   private const string TableFolder = "tables";

   public record Result(int Tables, int Rows, int Fields, IReadOnlyList<string> Notes);

   #region Export

   public static Result Export(IDataModel model, string directory) {
      Directory.CreateDirectory(directory);
      var tableDirectory = Path.Combine(directory, TableFolder);
      Directory.CreateDirectory(tableDirectory);

      var notes = new List<string>();
      int tables = 0, rows = 0, fields = 0;

      foreach (var (name, run) in NamedTables(model)) {
         var table = model.GetTableModel(name);
         if (table == null || table.Count == 0) continue;

         var segments = run.ElementContent;
         var text = new StringBuilder();
         text.AppendLine("# " + name);
         text.AppendLine("# exported by HexManiacAdvance - edit values, keep the columns");
         text.AppendLine(string.Join("\t", new[] { "index" }.Concat(segments.Select(s => s.Name))));

         for (int i = 0; i < table.Count; i++) {
            var element = table[i];
            var cells = new List<string> { i.ToString(CultureInfo.InvariantCulture) };
            foreach (var segment in segments) cells.Add(Format(element, segment));
            text.AppendLine(string.Join("\t", cells));
            rows++;
            fields += segments.Count;
         }

         File.WriteAllText(Path.Combine(tableDirectory, Sanitize(name) + ".tsv"), text.ToString());
         tables++;
      }

      var manifest = new StringBuilder();
      manifest.AppendLine("# HexManiacAdvance project export");
      manifest.AppendLine($"exported\t{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      manifest.AppendLine($"gameCode\t{model.GetGameCode()}");
      manifest.AppendLine($"romSize\t0x{model.Count:X}");
      manifest.AppendLine($"tables\t{tables}");
      File.WriteAllText(Path.Combine(directory, ManifestName), manifest.ToString());

      return new Result(tables, rows, fields, notes);
   }

   private static string Format(ModelArrayElement element, ArrayRunElementSegment segment) {
      try {
         return segment.Type switch {
            ElementContentType.PCS => Escape(element.GetStringValue(segment.Name)),
            // Written for context and for a readable diff; never imported - see the class comment.
            // Escaped like any other cell: a pointer to text returns the text, which routinely
            // contains newlines, and an unescaped one splits the row and loses the columns after it.
            ElementContentType.Pointer => "<" + Escape(element.GetStringValue(segment.Name)) + ">",
            _ => element.GetValue(segment.Name).ToString(CultureInfo.InvariantCulture),
         };
      } catch (Exception) {
         return "?";
      }
   }

   private static string Escape(string value) =>
      (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", string.Empty).Replace("\n", "\\n");

   private static string Unescape(string value) {
      var text = new StringBuilder();
      for (int i = 0; i < value.Length; i++) {
         if (value[i] != '\\' || i + 1 == value.Length) { text.Append(value[i]); continue; }
         i++;
         text.Append(value[i] switch { 't' => '\t', 'n' => '\n', '\\' => '\\', var c => c });
      }
      return text.ToString();
   }

   #endregion

   #region Import

   public static Result Import(IEditableViewPort viewPort, string directory) {
      var model = viewPort.Model;
      var notes = new List<string>();
      var manifestPath = Path.Combine(directory, ManifestName);
      if (!File.Exists(manifestPath)) throw new InvalidOperationException($"No {ManifestName} in {directory} - is this a project export?");

      var manifest = File.ReadAllLines(manifestPath)
         .Where(line => line.Contains('\t'))
         .ToDictionary(line => line.Split('\t')[0], line => line.Split('\t')[1]);

      // Refuse a mismatched ROM rather than writing values into the wrong tables.
      if (manifest.TryGetValue("gameCode", out var gameCode) && gameCode != model.GetGameCode()) {
         throw new InvalidOperationException(
            $"This export is for game code {gameCode}, but the open ROM is {model.GetGameCode()}.");
      }

      var tableDirectory = Path.Combine(directory, TableFolder);
      if (!Directory.Exists(tableDirectory)) throw new InvalidOperationException($"No {TableFolder} folder in {directory}.");

      int tables = 0, rows = 0, fields = 0;
      var token = viewPort.ChangeHistory.CurrentChange;

      foreach (var file in Directory.EnumerateFiles(tableDirectory, "*.tsv").OrderBy(f => f)) {
         var lines = File.ReadAllLines(file);
         var name = lines.FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim();
         if (name == null) { notes.Add($"{Path.GetFileName(file)}: no table name in the header, skipped"); continue; }

         var table = model.GetTableModel(name, () => token);
         if (table == null) { notes.Add($"{name}: not a table in this ROM, skipped"); continue; }

         var headerLine = lines.FirstOrDefault(l => l.StartsWith("index\t"));
         if (headerLine == null) { notes.Add($"{name}: no column header, skipped"); continue; }
         var columns = headerLine.Split('\t');

         var wroteAnything = false;
         var malformed = 0;
         foreach (var line in lines) {
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("index\t")) continue;
            var cells = line.Split('\t');
            if (cells.Length != columns.Length) { malformed++; continue; }
            if (!int.TryParse(cells[0], out var index) || index < 0 || index >= table.Count) continue;

            var element = table[index];
            for (int c = 1; c < columns.Length; c++) {
               var field = columns[c];
               if (!element.HasField(field)) continue;
               var segment = element.Table.ElementContent.FirstOrDefault(s => s.Name == field);
               if (segment == null) continue;
               if (ApplyValue(element, segment, cells[c])) { fields++; wroteAnything = true; }
            }
            rows++;
         }
         if (malformed > 0) notes.Add($"{name}: {malformed} row(s) had the wrong number of columns and were skipped");
         if (wroteAnything) tables++;
      }

      viewPort.ChangeHistory.ChangeCompleted();
      viewPort.Refresh();
      return new Result(tables, rows, fields, notes);
   }

   /// <returns>Whether the value was written. Unwritable kinds are skipped, not guessed at.</returns>
   private static bool ApplyValue(ModelArrayElement element, ArrayRunElementSegment segment, string cell) {
      try {
         switch (segment.Type) {
            case ElementContentType.PCS:
               var wanted = Unescape(cell);
               if (element.GetStringValue(segment.Name) == wanted) return false;
               element.SetStringValue(segment.Name, wanted);
               return true;
            case ElementContentType.Pointer:
               return false;   // context only; relocating a pointer's target is the editor's job
            default:
               if (!int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return false;
               if (element.GetValue(segment.Name) == number) return false;
               element.SetValue(segment.Name, number);
               return true;
         }
      } catch (Exception) {
         return false;
      }
   }

   #endregion

   private static IEnumerable<(string Name, ITableRun Run)> NamedTables(IDataModel model) {
      foreach (var run in model.Arrays) {
         var name = model.GetAnchorFromAddress(-1, run.Start);
         if (!string.IsNullOrEmpty(name)) yield return (name, run);
      }
   }

   private static string Sanitize(string name) {
      var invalid = Path.GetInvalidFileNameChars();
      return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
   }
}
