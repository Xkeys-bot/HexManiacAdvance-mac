using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Describes one quick edit and asks whether to run it. Ported from the Window that
/// HexManiac.WPF/Windows/MainWindow.xaml.cs built inline in CreateQuickEditCommand.
/// </summary>
public partial class QuickEditDialog : Window {
   private IQuickEditItem edit;

   /// <summary>True when the user pressed Run.</summary>
   public bool ShouldRun { get; private set; }

   public QuickEditDialog() => InitializeComponent();

   public QuickEditDialog(IQuickEditItem edit) : this() {
      this.edit = edit;
      Title = edit.Name;
      DescriptionText.Text = edit.Description;
      WikiLink.IsVisible = !string.IsNullOrEmpty(edit.WikiLink);
   }

   private void OpenWiki(object sender, RoutedEventArgs e) {
      if (!string.IsNullOrEmpty(edit?.WikiLink)) NativeProcess.Start(edit.WikiLink);
   }

   private void RunEdit(object sender, RoutedEventArgs e) {
      ShouldRun = true;
      Close();
   }

   private void CancelEdit(object sender, RoutedEventArgs e) => Close();
}
