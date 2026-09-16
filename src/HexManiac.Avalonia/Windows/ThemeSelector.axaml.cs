using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>Ported from HexManiac.WPF/Windows/ThemeSelector.xaml.cs.</summary>
public partial class ThemeSelector : Window {
   public ThemeSelector() {
      InitializeComponent();
      // WPF cleared keyboard focus on MouseLeftButtonDown so the swatch popups would dismiss.
      PointerPressed += (sender, e) => Focus();
   }

   private void CloseWindow(object sender, RoutedEventArgs e) {
      this.Close();
   }

   private void ThemeReset(object sender, RoutedEventArgs e) {
      var viewModel = (CoreTheme)DataContext;
      viewModel.Reset();
   }
}
