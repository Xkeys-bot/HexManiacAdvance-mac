using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>Ported from HexManiac.WPF/Windows/RequestTextDialog.xaml.cs.</summary>
public partial class RequestTextDialog : Window {
   public string Result { get; set; }

   public RequestTextDialog() {
      InitializeComponent();
      TextBox.KeyDown += (sender, e) => {
         if (e.Key == Key.Enter) { AcceptText(sender, e); e.Handled = true; }
      };
      Opened += (sender, e) => TextBox.Focus();
   }

   private void AcceptText(object sender, RoutedEventArgs e) {
      Result = TextBox.Text;
      Close(Result);
   }

   private void CancelText(object sender, RoutedEventArgs e) => Close(null);
}
