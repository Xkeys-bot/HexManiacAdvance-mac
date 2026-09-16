using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using HavenSoft.HexManiac.AvaloniaUI.Controls;
using HavenSoft.HexManiac.AvaloniaUI.Windows;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;

namespace HavenSoft.HexManiac.AvaloniaUI.Views;

/// <summary>
/// Picks the view for each tab. Hex, map, image and pokedex-reorder tabs are all ported;
/// anything else falls through to a placeholder. Add a case here per port.
/// </summary>
public class TabContentTemplate : IDataTemplate {
   public Control Build(object data) => data switch {
      // The starter's HexGrid stub is superseded by the real TabView + HexContent port.
      ViewPort viewPort => WireFocus(new TabView { DataContext = viewPort }, view => view.FocusElement += Focused),
      ImageEditorViewModel image => new ImageEditorView { DataContext = image },
      DexReorderTab dex => new DexReorderView { DataContext = dex },
      MapEditorViewModel map => WireFocus(new MapTab { DataContext = map }, view => view.FocusElement += Focused),
      null => new TextBlock(),
      _ => new TextBlock {
         Text = $"The {data.GetType().Name} editor hasn't been ported yet.",
         Margin = new Thickness(24),
         Opacity = 0.7,
      },
   };

   /// <summary>
   /// WPF's views raised a RoutedCommand that bubbled to MainWindow, which drew the focus
   /// animation. Avalonia has no routed commands, so the views expose a FocusElement event.
   /// Core's EventHandler.Raise extension passes the control to highlight as the event *source*,
   /// which is also how WPF read it, and the shell is found by walking up from that control.
   /// </summary>
   private static T WireFocus<T>(T view, Action<T> subscribe) where T : Control {
      subscribe(view);
      return view;
   }

   private static void Focused(object sender, EventArgs e) {
      if (sender is not Control element) return;
      if (element.GetVisualRoot() is MainWindow window) window.AnimateFocus(element);
   }

   public bool Match(object data) => true;
}
