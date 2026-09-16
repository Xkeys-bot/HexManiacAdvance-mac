using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.AvaloniaUI.Windows;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>Ported from HexManiac.WPF/Controls/StartScreen.xaml.cs.</summary>
public partial class StartScreen : Grid {
   public StartScreen() {
      InitializeComponent();
      AttachedToVisualTree += (sender, e) => {
         var version = (DataContext as EditorViewModel)?.Singletons.MetadataInfo;
         Usage.Text = AboutWindow.GetUsageText(version);
      };
   }

   /// WPF's Hyperlink.RequestNavigate carried the Uri; the ported buttons carry it in Tag.
   private void Navigate(object sender, RoutedEventArgs e) {
      if (sender is Control control && control.Tag is string url) {
         Process.Start(new ProcessStartInfo("open", $"\"{url}\"") { UseShellExecute = false });
      }
      e.Handled = true;
   }
}

/// <summary>
/// Causes the element within the decorator to not take part in the Measure step of the layout loop.
/// </summary>
public class MeasureEmptyDecorator : Decorator {
   protected override Size MeasureOverride(Size constraint) {
      Child?.Measure(default);
      return default;
   }
}
