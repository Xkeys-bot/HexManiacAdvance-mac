using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core.Models;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>Ported from HexManiac.WPF/Windows/AboutWindow.xaml.cs.</summary>
public partial class AboutWindow : Window {
   const string LicenseUrl = "https://github.com/haven1433/HexManiacAdvance/blob/master/LICENSE";

   public AboutWindow() : this(null) { }

   public AboutWindow(IMetadataInfo metadata) {
      InitializeComponent();
      Version.Text = $"Version {metadata?.VersionNumber}";
      Usage.Text = GetUsageText(metadata);
   }

   public static string GetUsageText(IMetadataInfo metadata) {
      if (metadata != null && metadata.IsPublicRelease) {
         return "This is a preview release. Please report bugs via GitHub / Discord.";
      } else {
         return "This is an unstable version: use at your own risk!";
      }
   }

   /// WPF used NativeProcess.Start on the Hyperlink's Uri; on macOS that is `open`.
   private void Navigate(object sender, RoutedEventArgs e) {
      Process.Start(new ProcessStartInfo("open", $"\"{LicenseUrl}\"") { UseShellExecute = false });
      e.Handled = true;
   }
}
