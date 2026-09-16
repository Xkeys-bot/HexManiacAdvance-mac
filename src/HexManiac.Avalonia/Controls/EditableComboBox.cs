using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/EditableComboBox.xaml(.cs).
///
/// WPF used ComboBox with IsEditable="True". Avalonia's ComboBox has no editable mode at all, so
/// the equivalent control is AutoCompleteBox, which is a text box with a filtered drop-down.
/// IndexComboBoxViewModel binds Options / Text / SelectedIndex, so SelectedIndex is reproduced
/// here on top of AutoCompleteBox's SelectedItem.
/// </summary>
public class EditableComboBox : AutoCompleteBox {

   public static readonly StyledProperty<int> SelectedIndexProperty =
      AvaloniaProperty.Register<EditableComboBox, int>(nameof(SelectedIndex), -1,
         defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

   public int SelectedIndex {
      get => GetValue(SelectedIndexProperty);
      set => SetValue(SelectedIndexProperty, value);
   }

   bool updatingSelection;

   static EditableComboBox() {
      SelectedIndexProperty.Changed.AddClassHandler<EditableComboBox>((self, e) => self.OnSelectedIndexChanged());
      SelectedItemProperty.Changed.AddClassHandler<EditableComboBox>((self, e) => self.OnSelectedItemChanged());
   }

   public EditableComboBox() {
      FilterMode = AutoCompleteFilterMode.Contains;
      // WPF set IsTextSearchEnabled="False"; the equivalent here is not to auto-append a completion.
      MinimumPrefixLength = 0;
      KeyDown += KeyDownToViewModel;
   }

   void OnSelectedIndexChanged() {
      if (updatingSelection) return;
      var options = ItemsSource?.Cast<object>().ToList();
      if (options == null) return;
      updatingSelection = true;
      SelectedItem = SelectedIndex >= 0 && SelectedIndex < options.Count ? options[SelectedIndex] : null;
      updatingSelection = false;
   }

   void OnSelectedItemChanged() {
      if (updatingSelection) return;
      var options = ItemsSource?.Cast<object>().ToList();
      if (options == null) return;
      updatingSelection = true;
      SelectedIndex = SelectedItem == null ? -1 : options.IndexOf(SelectedItem);
      updatingSelection = false;
   }

   private void KeyDownToViewModel(object sender, KeyEventArgs e) {
      var element = (StyledElement)sender;
      if (element.DataContext is not IndexComboBoxViewModel viewModel) return;
      if (e.Key == Key.Enter) viewModel.CompleteFilterInteraction();
   }
}
