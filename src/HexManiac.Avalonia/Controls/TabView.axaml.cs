using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HavenSoft.HexManiac.AvaloniaUI.Implementations;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/TabView.xaml.cs.
///
/// Recurring translations:
/// * MouseButtonEventArgs / MouseEventArgs -> PointerPressed/Moved/ReleasedEventArgs.
/// * CaptureMouse() / IsMouseCaptured / ReleaseMouseCapture() -> e.Pointer.Capture plus a
///   tracked flag, since Avalonia has no per-element IsMouseCaptured.
/// * KeyboardFocusChangedEventArgs -> GotFocusEventArgs.
/// * ScrollViewer.ScrollToVerticalOffset / VerticalOffset -> the Offset vector.
/// * PreviewX -> AddHandler(XEvent, ..., RoutingStrategies.Tunnel).
/// * TextBox.SelectionLength -> SelectionEnd - SelectionStart; ExtentHeight/ViewportHeight are
///   not exposed, so the scroll maths goes through the TextBox's own ScrollViewer where needed.
///
/// Not carried over: WPF's scroll animation (OldContent/OldHeader RenderTargetBitmap snapshots
/// blended under a DoubleAnimation). Avalonia's compositor re-renders the hex surface directly
/// and there is no BeginAnimation on a TranslateTransform, so AnimateScroll is a no-op here.
/// </summary>
public partial class TabView : UserControl {
   public event EventHandler FocusElement;

   #region ZoomLevel

   public static readonly StyledProperty<int> ZoomLevelProperty =
      AvaloniaProperty.Register<TabView, int>(nameof(ZoomLevel), 16, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

   public int ZoomLevel {
      get => GetValue(ZoomLevelProperty);
      set => SetValue(ZoomLevelProperty, value);
   }

   #endregion

   #region AnimateScroll

   public static readonly StyledProperty<bool> AnimateScrollProperty =
      AvaloniaProperty.Register<TabView, bool>(nameof(AnimateScroll), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

   public bool AnimateScroll {
      get => GetValue(AnimateScrollProperty);
      set => SetValue(AnimateScrollProperty, value);
   }

   #endregion

   public IFileSystem FileSystem =>
      Application.Current.Resources.TryGetResource("FileSystem", null, out var fs) ? fs as IFileSystem : null;

   private readonly DispatcherTimer timer;
   private IViewPort subscribedViewPort;
   private ViewPort subscribedFullViewPort;

   public TabView() {
      InitializeComponent();

      CodeModeSelector.ItemsSource = Enum.GetValues(typeof(CodeMode)).Cast<CodeMode>().ToList();
      timer = new DispatcherTimer(TimeSpan.FromSeconds(.6), DispatcherPriority.ApplicationIdle, BlinkCursor);
      timer.Stop();

      DataContextChanged += (sender, e) => HandleDataContextChanged();

      // WPF used PreviewKeyDown on the table panel; Avalonia spells that as a tunnelling handler.
      TableToolPanel.AddHandler(KeyDownEvent, TableKeyDown, RoutingStrategies.Tunnel);

      // WPF hooked DropDownClosed on both selectors so that re-picking the current table still
      // told the view model (it wants to Goto even when the index does not change).
      SectionSelector.DropDownClosed += TableSelected;
      TableSelector.DropDownClosed += TableSelected;

      StringToolTextBox.PropertyChanged += (sender, e) => {
         if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty) {
            StringToolContentSelectionChanged();
         }
      };

      AnchorTextBox.PropertyChanged += (sender, e) => {
         if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty) {
            AnchorSelectionChanged();
         }
      };

      // WPF: PreviewMouseDown="ClearPopup" on the UserControl, plus the UpdateInProgress gate.
      AddHandler(PointerPressedEvent, HandlePreviewPointerPressed, RoutingStrategies.Tunnel);
      AddHandler(KeyDownEvent, HandlePreviewKeyDown, RoutingStrategies.Tunnel);
      CodeToolBody.AddHandler(PointerWheelChangedEvent, WheelCodeBody, RoutingStrategies.Tunnel);
      CodeToolMultiTextBox.AddHandler(PointerWheelChangedEvent, ScrollCodeContent, RoutingStrategies.Tunnel);
   }

   #region DataContext plumbing

   private void HandleDataContextChanged() {
      if (subscribedViewPort != null) {
         subscribedViewPort.PropertyChanged -= HandleViewPortScrollChanged;
         SetViewModelScrollLocations(subscribedViewPort);
      }
      if (subscribedFullViewPort != null) {
         subscribedFullViewPort.Tools.StringTool.PropertyChanged -= HandleStringToolPropertyChanged;
         subscribedFullViewPort.FocusToolPanel -= FocusToolPanel;
         subscribedFullViewPort.Tools.CodeTool.AttentionNewContent -= FocusNewCodeContent;
         subscribedFullViewPort.PropertyChanged -= HandleViewportPropertyChanged;
      }

      subscribedViewPort = DataContext as IViewPort;
      subscribedFullViewPort = DataContext as ViewPort;

      if (subscribedViewPort != null) {
         subscribedViewPort.PropertyChanged += HandleViewPortScrollChanged;
         GetViewModelScrollLocations(subscribedViewPort);
      }
      if (subscribedFullViewPort != null) {
         subscribedFullViewPort.Tools.StringTool.PropertyChanged += HandleStringToolPropertyChanged;
         subscribedFullViewPort.FocusToolPanel += FocusToolPanel;
         subscribedFullViewPort.Tools.CodeTool.AttentionNewContent += FocusNewCodeContent;
         subscribedFullViewPort.PropertyChanged += HandleViewportPropertyChanged;
      }
   }

   private static void SetVerticalOffset(ScrollViewer viewer, double offset) {
      if (viewer == null) return;
      viewer.Offset = new Vector(viewer.Offset.X, offset);
   }

   private static ScrollViewer ScrollerOf(Control control) =>
      control?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

   private void GetViewModelScrollLocations(IViewPort viewPort) {
      if (viewPort.Tools?.TableTool != null) SetVerticalOffset(TableScrollViewer, viewPort.Tools.TableTool.VerticalOffset);
      if (viewPort.Tools?.StringTool != null) SetVerticalOffset(ScrollerOf(StringToolTextBox), viewPort.Tools.StringTool.VerticalOffset);
      if (viewPort.Tools?.CodeTool != null) {
         SetVerticalOffset(ScrollerOf(CodeToolSingleTextBox), viewPort.Tools.CodeTool.SingleBoxVerticalOffset);
         SetVerticalOffset(CodeToolMultiTextBox, viewPort.Tools.CodeTool.MultiBoxVerticalOffset);
      }
   }

   private void SetViewModelScrollLocations(IViewPort viewPort) {
      if (viewPort.Tools?.TableTool != null) viewPort.Tools.TableTool.VerticalOffset = TableScrollViewer.Offset.Y;
      if (viewPort.Tools?.StringTool != null) viewPort.Tools.StringTool.VerticalOffset = ScrollerOf(StringToolTextBox)?.Offset.Y ?? 0;
      if (viewPort.Tools?.CodeTool != null) {
         viewPort.Tools.CodeTool.SingleBoxVerticalOffset = ScrollerOf(CodeToolSingleTextBox)?.Offset.Y ?? 0;
         viewPort.Tools.CodeTool.MultiBoxVerticalOffset = CodeToolMultiTextBox.Offset.Y;
      }
   }

   /// WPF animated the hex surface on scroll. See the class remarks: there is no equivalent here,
   /// but the handler still exists so AnimateScroll keeps its meaning if it is ever wired up.
   private void HandleViewPortScrollChanged(object sender, PropertyChangedEventArgs e) { }

   #endregion

   #region Manual Selection Code

   private void HideManualSelection(object sender, RoutedEventArgs e) => ManualHighlight.IsVisible = false;

   private void ShowManualSelection(object sender, RoutedEventArgs e) {
      ManualHighlight.IsVisible = true;
      UpdateManualSelectionFromScroll();
   }

   private bool updatingStringToolSelection;

   private void HandleStringToolPropertyChanged(object sender, PropertyChangedEventArgs e) {
      var tool = (PCSTool)sender;
      updatingStringToolSelection = true;
      if (e.PropertyName is nameof(PCSTool.ContentIndex)) {
         var length = StringToolTextBox.SelectionEnd - StringToolTextBox.SelectionStart;
         StringToolTextBox.SelectionStart = tool.ContentIndex;
         StringToolTextBox.SelectionEnd = tool.ContentIndex + length;
         UpdateManualSelection();
      }
      if (e.PropertyName is nameof(PCSTool.ContentSelectionLength)) {
         StringToolTextBox.SelectionEnd = StringToolTextBox.SelectionStart + tool.ContentSelectionLength;
         UpdateManualSelection();
      }
      updatingStringToolSelection = false;
   }

   private void UpdateManualSelection() {
      var text = StringToolTextBox.Text ?? string.Empty;
      var scroller = ScrollerOf(StringToolTextBox);
      if (scroller == null) return;
      var start = Math.Min(text.Length, StringToolTextBox.SelectionStart);
      var linesBeforeSelection = text.Substring(0, start).Split(Environment.NewLine).Length - 1;
      var lastLineIndex = text.Split(Environment.NewLine).Length - 1;
      var highestScroll = Math.Max(scroller.Extent.Height - scroller.Viewport.Height, 0);
      var verticalOffset = lastLineIndex == 0 ? 0 : linesBeforeSelection * highestScroll / lastLineIndex;
      if (verticalOffset < 0) verticalOffset = 0;
      if (scroller.Offset.Y != verticalOffset) {
         SetVerticalOffset(scroller, verticalOffset);
      } else {
         UpdateManualSelectionFromScroll();
      }
   }

   private void UpdateManualSelectionFromScroll() {
      if (!ManualHighlight.IsVisible) return;
      if (StringToolTextBox.DataContext is not ToolTray tools || tools.StringTool == null) return;
      var tool = tools.StringTool;
      var scroller = ScrollerOf(StringToolTextBox);
      if (scroller == null) return;
      var text = StringToolTextBox.Text ?? string.Empty;

      var start = Math.Min(text.Length, StringToolTextBox.SelectionStart);
      var linesBeforeSelection = text.Substring(0, start).Split(Environment.NewLine).Length - 1;
      var totalLines = text.Split(Environment.NewLine).Length;
      var verticalOffset = scroller.Offset.Y;
      var lineHeight = scroller.Extent.Height / totalLines;
      var verticalStart = lineHeight * linesBeforeSelection - verticalOffset + 2;
      if (verticalStart < 0 || verticalStart > scroller.Viewport.Height) {
         ManualHighlight.Opacity = 0;
         return;
      }

      var selectionStart = text.Substring(0, start).Split(Environment.NewLine).Last().Length;
      const double fontWidth = 6.6;
      var horizontalStart = selectionStart * fontWidth + 2;
      var width = tool.ContentSelectionLength * fontWidth;
      ManualHighlight.Opacity = 0.4;
      ManualHighlight.Margin = new Thickness(horizontalStart, verticalStart, Math.Max(0, StringToolTextBox.Bounds.Width - horizontalStart - width), 0);
   }

   private void StringToolContentSelectionChanged() {
      if (updatingStringToolSelection) return;
      if (StringToolTextBox.DataContext is not ToolTray tools || tools.StringTool == null) return;
      var tool = tools.StringTool;
      tool.PropertyChanged -= HandleStringToolPropertyChanged;
      tool.ContentIndex = StringToolTextBox.SelectionStart;
      tool.ContentSelectionLength = StringToolTextBox.SelectionEnd - StringToolTextBox.SelectionStart;
      tool.PropertyChanged += HandleStringToolPropertyChanged;
   }

   #endregion

   #region Blink Cursor Code

   private static IBrush Brush(string name) =>
      Application.Current.Resources.TryGetResource(name, null, out var value) ? value as IBrush : Brushes.Transparent;

   private void UpdateBlinkyCursor(object sender, EventArgs e) {
      if (HexContent.ViewPort is not ViewPort viewPort || viewPort.UpdateInProgress) return;
      try {
         var screenPosition = HexContent.CursorLocation;
         if (screenPosition.X >= 0 && screenPosition.X < HexContent.Bounds.Width && screenPosition.Y >= 0 && screenPosition.Y < HexContent.Bounds.Height) {
            var dataPosition = viewPort.SelectionStart;
            var format = viewPort[dataPosition.X, dataPosition.Y].Format;
            double offset;
            if (format is UnderEdit edit) {
               offset = FormatDrawer.CalculateTextOffset(edit.CurrentText, HexContent.FontSize, HexContent.CellWidth, edit);
            } else {
               offset = FormatDrawer.CalculateTextOffset(string.Empty, HexContent.FontSize, HexContent.CellWidth, null);
            }
            BlinkyCursor.Margin = new Thickness(screenPosition.X + offset, screenPosition.Y, 0, 0);
            BlinkyCursor.Height = HexContent.CellHeight;
            BlinkyCursor.IsVisible = true;
         } else {
            BlinkyCursor.IsVisible = false;
         }
      } catch (Exception) {
         // The cursor blink is not important, and can run at basically any time - during an
         // external file change, or during mouse movement on initial load. Swallow and carry on.
      }
   }

   private void ShowCursor(object sender, RoutedEventArgs e) {
      if (HexContent.ViewPort is not ViewPort) return;
      BlinkyCursor.Fill = Brush(nameof(CoreTheme.Secondary));
      timer.Start();
   }

   private void HideCursor(object sender, RoutedEventArgs e) {
      if (HexContent.ViewPort is not ViewPort) return;
      timer.Stop();
      BlinkyCursor.Fill = Brushes.Transparent;
   }

   private void BlinkCursor(object sender, EventArgs e) {
      BlinkyCursor.Fill = ReferenceEquals(BlinkyCursor.Fill, Brushes.Transparent)
         ? Brush(nameof(CoreTheme.Secondary))
         : Brushes.Transparent;
   }

   #endregion

   #region Block Interactions

   /// WPF blocked input while a long operation was running, through OnPreviewMouseDown/KeyDown.
   private void HandlePreviewPointerPressed(object sender, PointerPressedEventArgs e) {
      ClearPopup();
      if (DataContext is IViewPort viewPort) e.Handled = viewPort.UpdateInProgress;
   }

   private void HandlePreviewKeyDown(object sender, KeyEventArgs e) {
      if (DataContext is IViewPort viewPort) e.Handled = viewPort.UpdateInProgress;
   }

   #endregion

   #region Anchor Text Selection

   private bool updatingAnchorSelection;

   /// When a change comes in from the UI.
   private void AnchorSelectionChanged() {
      if (updatingAnchorSelection) return;
      if (DataContext is not ViewPort viewPort) return;
      viewPort.PropertyChanged -= HandleViewportPropertyChanged;
      viewPort.AnchorTextSelectionStart = AnchorTextBox.SelectionStart;
      viewPort.AnchorTextSelectionLength = AnchorTextBox.SelectionEnd - AnchorTextBox.SelectionStart;
      viewPort.PropertyChanged += HandleViewportPropertyChanged;
   }

   /// When a change comes in from the ViewModel.
   private void HandleViewportPropertyChanged(object sender, PropertyChangedEventArgs e) {
      var viewPort = (ViewPort)sender;
      updatingAnchorSelection = true;
      if (e.PropertyName == nameof(viewPort.AnchorTextSelectionStart)) {
         AnchorTextBox.Focus();
         AnchorTextBox.SelectionStart = viewPort.AnchorTextSelectionStart;
      } else if (e.PropertyName == nameof(viewPort.AnchorTextSelectionLength)) {
         AnchorTextBox.SelectionEnd = AnchorTextBox.SelectionStart + viewPort.AnchorTextSelectionLength;
      }
      updatingAnchorSelection = false;
   }

   #endregion

   #region ColorSquareInteraction

   private void ShowColorFieldSwatch(object sender, PointerReleasedEventArgs e) {
      if (sender is not Control element) return;
      if (element.DataContext is not ColorFieldArrayElementViewModel field) return;
      var color = TileImage.Convert16BitColor(field.Color);
      var swatch = new Swatch { Width = 230, Height = 200, Result = color.ToString() };
      swatch.ResultChanged += (s, result) => {
         if (Color.TryParse(result, out var newColor)) field.Color = TileImage.Convert16BitColor(newColor);
      };
      // WPF built a transient Popup with StaysOpen=false; Avalonia's light dismiss is the same idea.
      var swatchPopup = new Popup {
         Placement = PlacementMode.Bottom,
         PlacementTarget = element,
         IsLightDismissEnabled = true,
         Child = swatch,
      };
      // A Popup only works once it is in the tree, so it is parked in the panel that owns the field.
      if (element.GetLogicalParent() is Panel parent) parent.Children.Add(swatchPopup);
      swatchPopup.IsOpen = true;
   }

   #endregion

   #region OffsetRender interactions

   private Avalonia.Point offsetRenderEditPoint;
   private bool offsetRenderCaptured;

   /// <summary>
   /// Source-pixel position, matching WPF. See MapTab.SourcePixelPosition for why: WPF scaled
   /// these images with a LayoutTransform, which leaves the element's own coordinates unscaled,
   /// while PixelImage here bakes SpriteScale into its layout size.
   /// </summary>
   private static Avalonia.Point SourcePixelPosition(Control element, PointerEventArgs e) {
      var p = e.GetPosition(element);
      var scale = (element.DataContext as IPixelViewModel)?.SpriteScale ?? 1;
      if (scale <= 0) scale = 1;
      return new Avalonia.Point(p.X / scale, p.Y / scale);
   }

   private void OffsetRenderMouseDown(object sender, PointerPressedEventArgs e) {
      if (sender is not Control element) return;
      e.Pointer.Capture(element);
      offsetRenderCaptured = true;
      offsetRenderEditPoint = SourcePixelPosition(element, e);
   }

   private void OffsetRenderMouseMove(object sender, PointerEventArgs e) {
      if (sender is not Control element) return;
      if (!offsetRenderCaptured) return;
      if (element.DataContext is not OffsetRenderViewModel viewModel) return;
      var newPoint = SourcePixelPosition(element, e);
      viewModel.ShiftDelta((int)(newPoint.X - offsetRenderEditPoint.X), (int)(newPoint.Y - offsetRenderEditPoint.Y));
      offsetRenderEditPoint = newPoint;
   }

   private void OffsetRenderMouseUp(object sender, PointerReleasedEventArgs e) {
      if (!offsetRenderCaptured) return;
      offsetRenderCaptured = false;
      e.Pointer.Capture(null);
   }

   #endregion

   #region Code Tool

   /// WPF positioned CodeContentsPopup by hand off the caret. The popup itself is declared in XAML.
   private void CodeToolContentsSelectionChanged(object sender, RoutedEventArgs e) {
      if (sender is not TextEditor editor) return;
      var textbox = editor.EditBox;
      if (DataContext is not IViewPort viewPort) return;
      if (viewPort.Tools?.CodeTool == null) return;
      if (editor.Tag is not CodeBody codebody) return;

      codebody.CaretPosition = textbox.SelectionStart;
      codebody.SelectedText = textbox.SelectedText;

      var text = textbox.Text ?? string.Empty;
      var linesBeforeSelection = text.Substring(0, Math.Min(text.Length, textbox.SelectionStart)).Split(Environment.NewLine).Length - 1;
      var lines = text.Split(Environment.NewLine);
      var lineHeight = textbox.Bounds.Height / Math.Max(lines.Length, 1);

      // WPF hid the help while the selection spans lines or the editor lost focus.
      var showHelp = !string.IsNullOrEmpty(codebody.HelpContent)
         && textbox.IsFocused
         && !(textbox.SelectedText ?? string.Empty).Contains(Environment.NewLine);

      // Float the insert-var / insert-flag buttons just past the end of the caret's line.
      var container = CodeToolMultiTextBoxItems.ContainerFromItem(codebody);
      if (container != null && linesBeforeSelection < lines.Length) {
         var typeface = new Typeface(textbox.FontFamily, FontStyle.Normal, FontWeight.Normal);
         var formatted = new FormattedText(lines[linesBeforeSelection], CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, textbox.FontSize, Brushes.Transparent);
         var transform = new TranslateTransform(formatted.Width + 16, lineHeight * linesBeforeSelection);
         foreach (var button in container.GetVisualDescendants().OfType<Button>()) {
            if (button.Name is "FloatingInsertVarButton" or "FloatingInsertFlagButton") button.RenderTransform = transform;
         }
         // WPF also suppressed the help popup whenever the autocomplete list was already showing.
         if (container.GetVisualDescendants().OfType<AutocompleteOverlay>().Any(overlay => overlay.IsVisible)) showHelp = false;
      }

      if (showHelp) ShowCodeHelp(codebody, lineHeight, linesBeforeSelection);
      else ClearPopup();
   }

   private void CodeBodyKeyDown(object sender, KeyEventArgs e) {
      if (sender is not Control element) return;
      if (element.Tag is not CodeBody body) return;
      if (e.Key == Key.U && e.KeyModifiers == HexContent.CommandModifier) {
         e.Handled = true;
         body.InsertFlagOrVar();
      }
   }

   private void WheelCodeBody(object sender, PointerWheelEventArgs e) {
      if (e.KeyModifiers != HexContent.CommandModifier) return;
      e.Handled = true;
      if (CodeToolBody.DataContext is not CodeTool context) return;
      context.FontSize = (Math.Sign(e.Delta.Y) + context.FontSize).LimitToRange(8, 20);
   }

   private void ScrollCodeContent(object sender, PointerWheelEventArgs e) => ClearPopup();

   private void ClearPopup() => CodeContentsPopup.IsOpen = false;

   /// <summary>
   /// WPF split CodeBody.HelpContent into a bold keyword, its arguments, and the documentation
   /// paragraph below. Same split here; only the placement differs (see the Popup in the XAML).
   /// </summary>
   private void ShowCodeHelp(CodeBody codebody, double lineHeight, int linesBeforeSelection) {
      var help = codebody.HelpContent;
      if (string.IsNullOrEmpty(help)) { ClearPopup(); return; }

      var helpParts = help.Split(new[] { Environment.NewLine }, 2, StringSplitOptions.None);
      var keyword = helpParts[0].Split(' ')[0];
      var args = helpParts[0].Split(new[] { ' ' }, 2).Last();
      if (keyword.StartsWith('\"')) {
         keyword += " " + args;
         args = string.Empty;
      }
      if (args == keyword) args = string.Empty;
      CodeContentsPopupKeywordText.Text = keyword;
      CodeContentsPopupArgsText.Text = " " + args;

      if (!help.Contains("#") && help.Trim().Contains(Environment.NewLine)) {
         CodeContentsPopupKeywordText.Text = string.Empty;
         CodeContentsPopupArgsText.Text = help.Trim();
         CodeContentsPopupDocumentationText.IsVisible = false;
      } else if (helpParts.Length == 1 || string.IsNullOrWhiteSpace(helpParts[1])) {
         CodeContentsPopupDocumentationText.IsVisible = false;
      } else {
         CodeContentsPopupDocumentationText.IsVisible = true;
         CodeContentsPopupDocumentationText.Text = helpParts[1];
      }

      CodeContentsPopup.VerticalOffset = lineHeight * (linesBeforeSelection + 1) + 2;
      CodeContentsPopup.HorizontalOffset = 40;
      CodeContentsPopup.IsOpen = true;
   }

   private void FocusToolPanel(object sender, EventArgs e) {
      if (DataContext is IViewPort viewPort && viewPort.Tools.SelectedTool is CodeTool code) {
         if (code.Mode.IsAny(CodeMode.Script, CodeMode.BattleScript, CodeMode.TrainerAiScript, CodeMode.AnimationScript)) {
            FocusElement.Raise(CodeToolMultiTextBoxItems);
            return;
         }
      }
      FocusElement.Raise(ToolPanel);
   }

   private void FocusNewCodeContent(object sender, EventArgs e) {
      if (DataContext is not ViewPort viewPort) return;
      var vm = viewPort.Tools.CodeTool.Contents.LastOrDefault();
      if (vm == null) return;
      // WPF: ItemContainerGenerator.ContainerFromItem.
      var visual = CodeToolMultiTextBoxItems.ContainerFromItem(vm);
      if (visual == null) return;
      FocusElement.Raise(visual);
   }

   #endregion

   private void HeaderMouseDown(object sender, PointerPressedEventArgs e) => HexContent.RaiseEvent(e);

   private void BytesShowMenu(object sender, PointerReleasedEventArgs e) {
      if (e.InitialPressMouseButton != MouseButton.Right) return;
      if (sender is not Control element) return;
      if (element.DataContext is not ViewPort viewModel) return;
      var menu = new ContextMenu();
      var item = new MenuItem { Header = "Copy Bytes" };
      item.Click += (s, args) => { viewModel.CopyBytes.Execute(FileSystem); menu.Close(); };
      menu.ItemsSource = new[] { item };
      menu.Open(element);
   }

   private void ResetLeftToolsPane(object sender, RoutedEventArgs e) => ToolPanel.Width = 500;

   /// <summary>
   /// If the current table is re-selected, the view model still wants to know about the input,
   /// so it can Goto the table.
   /// </summary>
   private void TableSelected(object sender, EventArgs e) {
      if (DataContext is not IViewPort viewPort) return;
      if (viewPort.Tools?.TableTool == null) return;
      viewPort.Tools.TableTool.SelectedTableSection = SectionSelector.SelectedIndex;
      viewPort.Tools.TableTool.SelectedTableIndex = TableSelector.SelectedIndex;
   }

   private void ActivatePalette(object sender, PointerPressedEventArgs e) {
      if (sender is Control element && element.DataContext is PaletteElementViewModel viewModel) viewModel.Activate();
   }

   private void CheckboxKeyUp(object sender, KeyEventArgs e) {
      if (e.Key != Key.Enter) return;
      if (sender is not CheckBox box) return;
      box.IsChecked = !(box.IsChecked ?? false);
      e.Handled = true;
   }

   /// WPF's InputBindings on the section header became Tapped/DoubleTapped handlers.
   private void ToggleSplitterVisibility(object sender, RoutedEventArgs e) {
      if (sender is Control element && element.DataContext is SplitterArrayElementViewModel vm) vm.ToggleVisibility.Execute();
   }

   private void PageMovePrevious(object sender, RoutedEventArgs e) => MovePage(e, vm => vm.MovePrevious());

   private void PageMoveNext(object sender, RoutedEventArgs e) => MovePage(e, vm => vm.MoveNext());

   private void MovePage(RoutedEventArgs e, Action<PagedElementViewModel> move) {
      if (e.Source is not Control element) return;
      if (element.DataContext is not PagedElementViewModel vm) return;
      var offset = TableScrollViewer.Offset;
      move(vm);

      // hack, carried over from WPF: the layout update that follows causes an undesired
      // auto-scroll, so the scroll is observed once and reversed.
      void HandleScrollChanged(object s, ScrollChangedEventArgs args) {
         TableScrollViewer.ScrollChanged -= HandleScrollChanged;
         TableScrollViewer.Offset = offset;
      }
      TableScrollViewer.ScrollChanged += HandleScrollChanged;
   }

   private void UpdateFieldValue(object sender, KeyEventArgs e) {
      if (sender is not Control element) return;
      if (element.DataContext is not FieldArrayElementViewModel vm) return;
      if (e.Key == Key.Up) vm.IncrementValue();
      if (e.Key == Key.Down) vm.DecrementValue();
   }

   private void HandleFieldKeyboardFocus(object sender, GotFocusEventArgs e) {
      if (sender is Control element && element.DataContext is FieldArrayElementViewModel vm) vm.Focus();
   }

   private void HandleTupleFieldKeyboardFocus(object sender, GotFocusEventArgs e) {
      if (sender is Control element && element.DataContext is NumericTupleElementViewModel vm) vm.Focus();
   }

   private void TableKeyDown(object sender, KeyEventArgs e) {
      if (DataContext is not ViewPort viewPort) return;
      if (e.Key == Key.PageUp && e.KeyModifiers == KeyModifiers.None) {
         e.Handled = true;
         viewPort.Tools.TableTool.Previous.Execute();
      } else if (e.Key == Key.PageDown && e.KeyModifiers == KeyModifiers.None) {
         e.Handled = true;
         viewPort.Tools.TableTool.Next.Execute();
      }
   }
}
