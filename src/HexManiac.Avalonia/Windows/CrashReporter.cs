using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Ported from the crash-reporting half of HexManiac.WPF/Windows/MainWindow.xaml.cs
/// (HandleException / AppendGeneralAppInfo / AppendException / ExtractExceptionInfo).
///
/// Split into its own file because MainWindow.axaml.cs is already the largest file in the port,
/// and because Avalonia raises unhandled exceptions from two places rather than WPF's single
/// DispatcherUnhandledException, so both entry points call in here.
///
/// The WPF version also offered to turn off hardware acceleration when the render thread failed
/// ("SyncFlush" in the stack). That is an HwndTarget.RenderMode change with no Avalonia analog -
/// the render mode is fixed when the platform is initialized in Program.cs - so a render failure
/// falls through to the normal report instead.
/// </summary>
public static class CrashReporter {
   public static void Report(EditorViewModel editor, IFileSystem fileSystem, string windowTitle, Exception exception) {
      if (exception == null) return;
      var version = editor?.Singletons?.MetadataInfo?.VersionNumber ?? "unknown";

      var text = new StringBuilder();
      text.AppendLine("Version Number: " + version);
#if DEBUG
      text.AppendLine("Debug Version");
#else
      text.AppendLine("Release Version");
#endif
      text.AppendLine(DateTime.Now.ToString());
      text.AppendLine("General Information:");
      AppendGeneralAppInfo(text, editor);

      var logLines = new List<string>();
      if (editor?.SelectedTab is IViewPort vp && vp.Tools is ToolTray tray && tray.LogTool is LogTool logs) {
         text.AppendLine("Recent Log Information:");
         logLines.AddRange(logs.LogMessages);
      } else if (editor?.SelectedTab is MapEditorViewModel mapEditor && mapEditor.ViewPort.Tools is ToolTray mapTray && mapTray.LogTool is LogTool mapLogs) {
         logLines.AddRange(mapLogs.LogMessages);
      }
      while (logLines.Count > 15) logLines.RemoveAt(1);
      if (logLines.Count > 0) text.AppendLine(Environment.NewLine.Join(logLines));

      text.AppendLine("Exception Information:");
      AppendException(text, exception);
      text.AppendLine("-------------------------------------------");
      text.AppendLine(Environment.NewLine);

      try {
         File.AppendAllText("crash.log", text.ToString());
      } catch (IOException) {
         // reporting the crash must never throw a second one
      } catch (UnauthorizedAccessException) {
      }

      var tabCount = editor?.Count ?? 0;
      var shortError = Environment.NewLine.Join(text.ToString().SplitLines().Take(15 + tabCount * 5 + logLines.Count));
      shortError = Environment.NewLine.Join(new[] {
         $"~I got a crash! ({version})",
         "```",
         shortError + "...",
         "```",
         "Let me tell you what I was doing right before I got the crash:",
      });

      var exceptionInfo = ExtractExceptionInfo(exception);
      fileSystem.ShowCustomMessageBox(
         "An unhandled error occured. Please report it on Discord or open an issue on GitHub." + Environment.NewLine +
         windowTitle + " might be in a bad state. You should close as soon as possible." + Environment.NewLine +
         "Here's a summary of the issue:" + Environment.NewLine +
         Environment.NewLine +
         exceptionInfo + Environment.NewLine +
         "The error has been logged to crash.log" + Environment.NewLine +
         "You may want to:",
         showYesNoCancel: false,
         // WPF said "Show crash.log in Explorer"; on macOS the same ProcessModel path opens Finder.
         new ProcessModel("Show crash.log in Finder", "/" + Path.GetFullPath("crash.log")),
         new ProcessModel("Report this via Discord", "https://discord.gg/Re6E6ePpFc"),
         new ProcessModel(
            "Report this via GitHub",
            "https://github.com/haven1433/HexManiacAdvance/issues/new?body=" + HttpUtility.UrlEncode(
               "*(Please replace this section with notes about what you were doing or how to reproduce)*" + Environment.NewLine +
               Environment.NewLine + Environment.NewLine + Environment.NewLine + Environment.NewLine +
               "Notes from crash.log: " + Environment.NewLine + Environment.NewLine +
               "    " + text.ToString().Replace(Environment.NewLine, Environment.NewLine + "    ")
            )
         ),
         new ProcessModel("Copy a crash message to the clipboard", shortError)
      );
   }

   private static string ExtractExceptionInfo(Exception ex) {
      var exceptionTypeShortName = ex.GetType().ToString().Split('.').Last().Split("Exception").First();
      var info = exceptionTypeShortName + ":" + Environment.NewLine + $"{ex.Message}" + Environment.NewLine;
      if (ex is AggregateException ag) {
         foreach (var e in ag.InnerExceptions) info += ExtractExceptionInfo(e);
      }
      return info;
   }

   private static void AppendException(StringBuilder text, Exception ex, int indent = 0) {
      var lineStart = new string(' ', indent);

      text.AppendLine(lineStart + ex.GetType().ToString());
      text.AppendLine(lineStart + ex.Message);
      if (ex is ArgumentOutOfRangeException aoore) text.AppendLine(lineStart + aoore.ActualValue?.ToString() ?? "<null>");
      if (ex is AggregateException ae) {
         foreach (var e in ae.InnerExceptions) AppendException(text, e, indent + 2);
      }

      text.AppendLine(ex.StackTrace + Environment.NewLine);
   }

   private static void AppendGeneralAppInfo(StringBuilder text, EditorViewModel editor) {
      if (editor == null) {
         text.AppendLine("No Editor ViewModel found.");
         return;
      }
      text.AppendLine("Current tab count: " + editor.Count);
      text.AppendLine("Current selected tab: " + editor.SelectedIndex);
      text.AppendLine("---");
      foreach (var tab in editor) {
         if (tab is IEditableViewPort viewPort) {
            text.AppendLine("Tab is ViewPort for " + Path.GetFileName(viewPort.FileName));
            text.AppendLine("Game Code: " + viewPort.Model.GetGameCode());
            text.AppendLine("Data Length: 0x" + viewPort.Model.Count.ToAddress());
            text.AppendLine("Pokemon Count: " + (viewPort.Model.GetTable(HardcodeTablesModel.PokemonNameTable)?.ElementCount ?? 0));
            text.AppendLine("---");
         } else if (tab is MapEditorViewModel mapEditor && mapEditor.PrimaryMap is not null) {
            var layout = new LayoutModel(mapEditor.PrimaryMap.GetLayout());
            var events = mapEditor.PrimaryMap.EventGroup;
            text.AppendLine("Tab is map for " + mapEditor.PrimaryMap.Name);
            text.AppendLine($"Size: {layout.Width}x{layout.Height}");
            if (events == null) text.AppendLine("(No Events)");
            else text.AppendLine($"Events: {events.Objects.Count}-{events.Warps.Count}-{events.Scripts.Count}-{events.Signposts.Count}");
            text.AppendLine("---");
         } else {
            text.AppendLine($"Tab is {tab.GetType()}");
            text.AppendLine(tab.Name);
            text.AppendLine("---");
         }
      }
   }
}
