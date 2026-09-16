using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/AngleComboBox.xaml(.cs).
/// The WPF .xaml was an empty shell declaring only the base type, so the Avalonia port is
/// code-only; the visual template lives in GeneralResources.axaml, as it did in WPF.
/// DependencyProperty becomes StyledProperty.
/// </summary>
public class AngleComboBox : ComboBox {

   #region AngleDirection

   public static readonly StyledProperty<AngleDirection> DirectionProperty =
      AvaloniaProperty.Register<AngleComboBox, AngleDirection>(nameof(Direction), AngleDirection.None);

   public AngleDirection Direction {
      get => GetValue(DirectionProperty);
      set => SetValue(DirectionProperty, value);
   }

   #endregion

   #region LeftTop

   public static readonly StyledProperty<Point> LeftTopProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(LeftTop), new Point(0, 0));

   public Point LeftTop {
      get => GetValue(LeftTopProperty);
      set => SetValue(LeftTopProperty, value);
   }

   public static Point GetLeftTop(AvaloniaObject obj) => obj.GetValue(LeftTopProperty);
   public static void SetLeftTop(AvaloniaObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

   #endregion

   #region LeftMiddle

   public static readonly StyledProperty<Point> LeftMiddleProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(LeftMiddle), new Point(0, 5));

   public Point LeftMiddle {
      get => GetValue(LeftMiddleProperty);
      set => SetValue(LeftMiddleProperty, value);
   }

   public static Point GetLeftMiddle(AvaloniaObject obj) => obj.GetValue(LeftMiddleProperty);
   public static void SetLeftMiddle(AvaloniaObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

   #endregion

   #region LeftBottom

   public static readonly StyledProperty<Point> LeftBottomProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(LeftBottom), new Point(0, 10));

   public Point LeftBottom {
      get => GetValue(LeftBottomProperty);
      set => SetValue(LeftBottomProperty, value);
   }

   public static Point GetLeftBottom(AvaloniaObject obj) => obj.GetValue(LeftBottomProperty);
   public static void SetLeftBottom(AvaloniaObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

   #endregion

   #region RightTop

   public static readonly StyledProperty<Point> RightTopProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(RightTop), new Point(0, 0));

   public Point RightTop {
      get => GetValue(RightTopProperty);
      set => SetValue(RightTopProperty, value);
   }

   public static Point GetRightTop(AvaloniaObject obj) => obj.GetValue(RightTopProperty);
   public static void SetRightTop(AvaloniaObject obj, Point value) => obj.SetValue(RightTopProperty, value);

   #endregion

   #region RightMiddle

   public static readonly StyledProperty<Point> RightMiddleProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(RightMiddle), new Point(0, 5));

   public Point RightMiddle {
      get => GetValue(RightMiddleProperty);
      set => SetValue(RightMiddleProperty, value);
   }

   public static Point GetRightMiddle(AvaloniaObject obj) => obj.GetValue(RightMiddleProperty);
   public static void SetRightMiddle(AvaloniaObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

   #endregion

   #region RightBottom

   public static readonly StyledProperty<Point> RightBottomProperty =
      AvaloniaProperty.Register<AngleComboBox, Point>(nameof(RightBottom), new Point(0, 10));

   public Point RightBottom {
      get => GetValue(RightBottomProperty);
      set => SetValue(RightBottomProperty, value);
   }

   public static Point GetRightBottom(AvaloniaObject obj) => obj.GetValue(RightBottomProperty);
   public static void SetRightBottom(AvaloniaObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

   #endregion

   #region HasOverflow

   /// WPF had a HasOverflow trigger that showed the full text as a tooltip when it was clipped.
   public static readonly StyledProperty<bool> HasOverflowProperty =
      AvaloniaProperty.Register<AngleComboBox, bool>(nameof(HasOverflow));

   public bool HasOverflow {
      get => GetValue(HasOverflowProperty);
      set => SetValue(HasOverflowProperty, value);
   }

   #endregion

   private FilteringComboOptions subscribed;
   private TextBox editableTextBox;

   /// <summary>
   /// WPF wired these bindings in the DataContextChanged handler because a FilteringComboOptions
   /// drives the control entirely from its own properties - every call site just sets DataContext.
   /// Avalonia has no DataContextChanged event argument pair, so the old view model is tracked here.
   /// </summary>
   public AngleComboBox() {
      DataContextChanged += (sender, e) => UpdateFilterBindings();
      AddHandler(KeyDownEvent, KeyDownToViewModel, RoutingStrategies.Tunnel);
      PointerEntered += (sender, e) => UpdateOverflow();
   }

   private void UpdateFilterBindings() {
      if (subscribed != null) subscribed.PropertyChanged -= HandleVMTextChanged;
      subscribed = DataContext as FilteringComboOptions;
      Classes.Set("editable", subscribed?.CanFilter ?? false);
      if (subscribed == null) return;
      subscribed.PropertyChanged += HandleVMTextChanged;
      Bind(ItemsSourceProperty, new Binding(nameof(FilteringComboOptions.FilteredOptions)));
      // Same guard the XAML selection bindings use: when this combo is unloaded its ItemsSource
      // detaches, SelectedIndex is coerced to -1, and a TwoWay binding would push that back.
      Bind(SelectedIndexProperty, new Binding(nameof(FilteringComboOptions.SelectedIndex), BindingMode.TwoWay) {
         Converter = IgnoreEmptySelectionConverter.Instance,
      });
      Bind(IsDropDownOpenProperty, new Binding(nameof(FilteringComboOptions.DropDownIsOpen), BindingMode.TwoWay));
      if (editableTextBox != null) {
         editableTextBox.Bind(TextBox.TextProperty, new Binding(nameof(FilteringComboOptions.DisplayText), BindingMode.TwoWay));
      }
   }

   protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
      base.OnApplyTemplate(e);
      editableTextBox = e.NameScope.Find<TextBox>("PART_EditableTextBox");
      if (editableTextBox != null && subscribed != null) {
         editableTextBox.Bind(TextBox.TextProperty, new Binding(nameof(FilteringComboOptions.DisplayText), BindingMode.TwoWay));
      }
   }

   private void HandleVMTextChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(FilteringComboOptions.CanFilter)) Classes.Set("editable", subscribed?.CanFilter ?? false);
      if (e.PropertyName != nameof(FilteringComboOptions.DisplayText)) return;
      ClearSelection();
   }

   /// WPF put the caret at the end of the text so typing appends instead of replacing.
   private void ClearSelection() {
      if (editableTextBox == null) return;
      editableTextBox.SelectionStart = editableTextBox.SelectionStart + editableTextBox.SelectionEnd - editableTextBox.SelectionStart;
      editableTextBox.SelectionEnd = editableTextBox.SelectionStart;
   }

   /// WPF measured the editable text box's extent against its viewport; Avalonia exposes neither,
   /// so the comparison is the presenter's desired width against the space it was given.
   private void UpdateOverflow() {
      if (editableTextBox == null) return;
      HasOverflow = editableTextBox.DesiredSize.Width > editableTextBox.Bounds.Width;
   }

   private void KeyDownToViewModel(object sender, KeyEventArgs e) {
      if (DataContext is FilteringComboOptions vm) {
         if (e.Key == Key.Up) vm.SelectUp();
         if (e.Key == Key.Down) vm.SelectDown();
         if (e.Key == Key.Enter) vm.SelectConfirm();
         if (e.Key.IsAny(Key.Up, Key.Down, Key.Enter)) e.Handled = true;
      }
      if (DataContext is not IndexComboBoxViewModel viewModel) return;
      if (e.Key == Key.Enter) viewModel.CompleteFilterInteraction();
   }
}
