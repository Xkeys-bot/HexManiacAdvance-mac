using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/PythonPanel.xaml.cs.
/// WPF wired PreviewKeyDown / PreviewMouseWheel in the .xaml; Avalonia attaches the equivalent
/// tunnelling handlers in the constructor, and Ctrl becomes the platform command key on macOS.
/// </summary>
public partial class PythonPanel : UserControl {
   public PythonPanel() {
      InitializeComponent();
      InputBox.AddHandler(KeyDownEvent, PythonTextKeyDown, RoutingStrategies.Tunnel);
      InputBox.AddHandler(PointerWheelChangedEvent, ChangeInputTextSize, RoutingStrategies.Tunnel);
   }


   private void PythonTextKeyDown(object sender, KeyEventArgs e) {
      if (e.Key == Key.Enter && e.KeyModifiers == HexContent.CommandModifier) {
         e.Handled = true;
         if (DataContext is not PythonTool tool) return;
         tool.RunPython();
      } else if (e.Key == Key.Escape) {
         e.Handled = true;
         if (DataContext is not PythonTool tool) return;
         tool.Close();
      }
   }

   private void ChangeInputTextSize(object sender, PointerWheelEventArgs e) {
      if (e.KeyModifiers != HexContent.CommandModifier) return;
      var box = (TextEditor)sender;
      e.Handled = true;
      box.FontSize = (box.FontSize + Math.Sign(e.Delta.Y)).LimitToRange(8, 30);
   }
}
