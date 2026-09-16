using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core.Models;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Ported from HexManiac.WPF/Windows/OptionDialog.xaml.cs.
/// WPF set DialogResult to close a modal Window; Avalonia closes with a result value instead,
/// so Result is both returned through Close() and left on the property for the WPF-shaped API.
/// </summary>
public partial class OptionDialog : Window {
   public int Result { get; set; }

   public OptionDialog() {
      InitializeComponent();
      Result = -1;
   }

   private void OptionClicked(object sender, RoutedEventArgs e) {
      var option = (VisualOption)((Control)sender).DataContext;
      Result = option.Index;
      Close(Result);
   }

   private void CancelClicked(object sender, RoutedEventArgs e) => Close(Result);
}
