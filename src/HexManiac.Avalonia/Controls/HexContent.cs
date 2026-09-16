using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HavenSoft.HexManiac.AvaloniaUI.Implementations;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System.Windows.Input;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;
using ScreenPoint = Avalonia.Point;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/HexContent.cs.
///
/// The drawing code is a direct translation (OnRender -> Render, matched Push/Pop -> using
/// scopes). The input layer is where Avalonia differs most: mouse events become pointer events,
/// mouse capture goes through e.Pointer.Capture, PreviewKeyDown becomes a tunnelling handler,
/// InputBindings become KeyBindings, and the tooltip is driven through the ToolTip attached
/// properties instead of ToolTipService.
/// </summary>
public class HexContent : Control {
   private static Pen borderPen;
   public static Pen BorderPen => borderPen ??= new Pen(ThemeDictionary.Brush(nameof(CoreTheme.Stream2)), 1);

   private Popup recentMenu;
   private ModelPoint downPoint;
   private ModelPoint mouseOverPoint;
   private bool isPointerCaptured;
   private bool middleButtonDown;

   #region ViewPort

   public IViewPort ViewPort {
      get => GetValue(ViewPortProperty);
      set => SetValue(ViewPortProperty, value);
   }

   public static readonly StyledProperty<IViewPort> ViewPortProperty =
      AvaloniaProperty.Register<HexContent, IViewPort>(nameof(ViewPort));

   private void OnViewPortChanged(AvaloniaPropertyChangedEventArgs e) {
      if (e.OldValue is IViewPort oldViewPort) {
         oldViewPort.CollectionChanged -= OnViewPortContentChanged;
         oldViewPort.PropertyChanged -= OnViewPortPropertyChanged;
         oldViewPort.RequestMenuClose -= OnViewPortRequestMenuClose;
         oldViewPort.Headers.CollectionChanged -= OnViewPortContentChanged;
      }

      if (e.NewValue is IViewPort newViewPort) {
         newViewPort.CollectionChanged += OnViewPortContentChanged;
         newViewPort.PropertyChanged += OnViewPortPropertyChanged;
         newViewPort.RequestMenuClose += OnViewPortRequestMenuClose;
         newViewPort.Headers.CollectionChanged += OnViewPortContentChanged;
         UpdateViewPortSize();
      }

      InvalidateVisual();
      CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
   }

   private void OnViewPortContentChanged(object sender, NotifyCollectionChangedEventArgs e) {
      InvalidateVisual();
      CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
   }

   private void OnViewPortPropertyChanged(object sender, PropertyChangedEventArgs e) {
      var propertyChangesThatRequireRedraw = new[] {
         nameof(Core.ViewModels.ViewPort.SelectionStart),
         nameof(Core.ViewModels.ViewPort.SelectionEnd),
         nameof(Core.ViewModels.ViewPort.ScrollValue),
         nameof(Core.ViewModels.ViewPort.UpdateInProgress),
      };

      if (propertyChangesThatRequireRedraw.Contains(e.PropertyName)) {
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      var propertyChangesThatRequireResize = new[] {
         nameof(IViewPort.StretchData),
         nameof(IViewPort.AllowMultipleElementsPerLine),
         nameof(Core.ViewModels.ViewPort.PreferredWidth),
      };

      if (propertyChangesThatRequireResize.Contains(e.PropertyName)) {
         UpdateViewPortSize();
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      if (e.PropertyName == nameof(Core.ViewModels.ViewPort.UpdateInProgress) && !ViewPort.UpdateInProgress) {
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      } else if (e.PropertyName == nameof(IEditableViewPort.SelectionEnd)) {
         MakeSelectionEndOnScreen();
      }
   }

   private void OnViewPortRequestMenuClose(object sender, EventArgs e) {
      if (ContextMenu != null) ContextMenu.Close();
      if (recentMenu == null) return;
      recentMenu.IsOpen = false;
   }

   #endregion

   #region CellWidth / Cell Height

   public static readonly StyledProperty<double> CellWidthProperty =
      AvaloniaProperty.Register<HexContent, double>(nameof(CellWidth), 0.0);

   public double CellWidth {
      get => GetValue(CellWidthProperty);
      set => SetValue(CellWidthProperty, value);
   }

   public static readonly StyledProperty<double> CellHeightProperty =
      AvaloniaProperty.Register<HexContent, double>(nameof(CellHeight), 0.0);

   public double CellHeight {
      get => GetValue(CellHeightProperty);
      set => SetValue(CellHeightProperty, value);
   }

   #endregion

   public event EventHandler CursorNeedsUpdate;

   #region FontSize

   public static readonly StyledProperty<int> FontSizeProperty =
      AvaloniaProperty.Register<HexContent, int>(nameof(FontSize), 16, defaultBindingMode: BindingMode.TwoWay);

   public int FontSize {
      get => GetValue(FontSizeProperty);
      set => SetValue(FontSizeProperty, value);
   }

   private void OnFontSizeChanged() {
      UpdateViewPortSize();
      InvalidateVisual();
      CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
   }

   #endregion

   #region ShowGrid

   public bool ShowGrid {
      get => GetValue(ShowGridProperty);
      set => SetValue(ShowGridProperty, value);
   }

   public static readonly StyledProperty<bool> ShowGridProperty =
      AvaloniaProperty.Register<HexContent, bool>(nameof(ShowGrid), false);

   private void OnRequestInvalidateVisual() {
      InvalidateVisual();
      CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
   }

   #endregion

   #region ShowHorizontalScroll

   public bool ShowHorizontalScroll {
      get => GetValue(ShowHorizontalScrollProperty);
      set => SetValue(ShowHorizontalScrollProperty, value);
   }

   public static readonly StyledProperty<bool> ShowHorizontalScrollProperty =
      AvaloniaProperty.Register<HexContent, bool>("ShowHorizontalScroll", false);

   #endregion

   #region HorizontalScrollValue

   public double HorizontalScrollValue {
      get => GetValue(HorizontalScrollValueProperty);
      set => SetValue(HorizontalScrollValueProperty, value);
   }

   public static readonly StyledProperty<double> HorizontalScrollValueProperty =
      AvaloniaProperty.Register<HexContent, double>("HorizontalScrollValue", 0.0);

   /// <summary>
   /// WPF ran a 200ms DoubleAnimation with DecelerationRatio 1 on this property. Avalonia's
   /// equivalent is a transition on the property, installed in the constructor, so assigning the
   /// value animates it with the same duration and easing.
   /// </summary>
   private void MakeSelectionEndOnScreen() {
      if (!(ViewPort is IEditableViewPort viewPort)) return;
      var x = viewPort.SelectionEnd.X;
      var newDesiredValue = HorizontalScrollValue;
      if (HorizontalScrollValue > x * CellWidth) {
         // can't see left edge of cell
         newDesiredValue = x * CellWidth;
      }
      if (newDesiredValue + Bounds.Width < (x + 1) * CellWidth) {
         // can't see right edge of cell
         newDesiredValue = (x + 1) * CellWidth - Bounds.Width;
      }

      if (newDesiredValue != HorizontalScrollValue) HorizontalScrollValue = newDesiredValue;
   }

   #endregion

   #region HorizontalScrollMaximum

   public double HorizontalScrollMaximum {
      get => GetValue(HorizontalScrollMaximumProperty);
      set => SetValue(HorizontalScrollMaximumProperty, value);
   }

   /// WPF read this out of the main window's resources; same idea, Avalonia lookup.
   public IFileSystem FileSystem {
      get {
         var top = TopLevel.GetTopLevel(this);
         if (top != null && top.TryFindResource("FileSystem", out var resource)) return resource as IFileSystem;
         return null;
      }
   }

   public static readonly StyledProperty<double> HorizontalScrollMaximumProperty =
      AvaloniaProperty.Register<HexContent, double>("HorizontalScrollMaximum", 0.0);

   #endregion

   #region SearchByte

   public static readonly StyledProperty<byte[]> SearchBytesProperty =
      AvaloniaProperty.Register<HexContent, byte[]>(nameof(SearchBytes));

   public byte[] SearchBytes {
      get => GetValue(SearchBytesProperty);
      set => SetValue(SearchBytesProperty, value);
   }

   #endregion

   #region DesiredHorizontalViewportSize

   public static readonly StyledProperty<double> DesiredHorizontalViewportSizeProperty =
      AvaloniaProperty.Register<HexContent, double>(nameof(DesiredHorizontalViewportSize), 0.0);

   public double DesiredHorizontalViewportSize {
      get => GetValue(DesiredHorizontalViewportSizeProperty);
      set => SetValue(DesiredHorizontalViewportSizeProperty, value);
   }

   #endregion

   public ScreenPoint CursorLocation {
      get {
         if (!(ViewPort is ViewPort viewPort)) return new ScreenPoint(-1, -1);
         var selection = viewPort.SelectionStart;
         return new ScreenPoint(selection.X * CellWidth - HorizontalScrollValue, selection.Y * CellHeight);
      }
   }

   static HexContent() {
      ViewPortProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnViewPortChanged(e));
      FontSizeProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnFontSizeChanged());
      ShowGridProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnRequestInvalidateVisual());
      ShowHorizontalScrollProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnRequestInvalidateVisual());
      HorizontalScrollValueProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnRequestInvalidateVisual());
      SearchBytesProperty.Changed.AddClassHandler<HexContent>((self, e) => self.OnRequestInvalidateVisual());
      FocusableProperty.OverrideDefaultValue<HexContent>(true);
   }

   public HexContent() {
      ClipToBounds = true;
      Focusable = true;

      Transitions = new Transitions {
         new DoubleTransition {
            Property = HorizontalScrollValueProperty,
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
         }
      };

      void AddKeyCommand(string commandPath, object arg, Key key, KeyModifiers modifiers = KeyModifiers.None) {
         var keyBinding = new KeyBinding { CommandParameter = arg, Gesture = new KeyGesture(key, modifiers) };
         // WPF's InputBindings inherited the element's DataContext. An Avalonia KeyBinding is not
         // in the visual tree and has none, so the path is anchored to this control's ViewPort.
         keyBinding.Bind(KeyBinding.CommandProperty, new Binding($"{nameof(ViewPort)}.{commandPath}") { Source = this });
         KeyBindings.Add(keyBinding);
      }

      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.PageUp, Key.PageUp);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.PageDown, Key.PageDown);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Home, Key.Home);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.End, Key.End);

      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.PageUp, Key.PageUp, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.PageDown, Key.PageDown, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Home, Key.Home, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.End, Key.End, KeyModifiers.Shift);

      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Up, Key.Up);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Down, Key.Down);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Left, Key.Left);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Right, Key.Right);

      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Up, Key.Up, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Down, Key.Down, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Left, Key.Left, KeyModifiers.Shift);
      AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Right, Key.Right, KeyModifiers.Shift);

      // PORTING.md's Ctrl -> Meta rule: these are Cmd chords on macOS.
      AddKeyCommand(nameof(IViewPort.Scroll), Direction.Up, Key.Up, CommandModifier);
      AddKeyCommand(nameof(IViewPort.Scroll), Direction.Down, Key.Down, CommandModifier);
      AddKeyCommand(nameof(IViewPort.Scroll), Direction.Left, Key.Left, CommandModifier);
      AddKeyCommand(nameof(IViewPort.Scroll), Direction.Right, Key.Right, CommandModifier);

      AddKeyCommand(nameof(IViewPort.Undo), null, Key.Z, CommandModifier);
      AddKeyCommand(nameof(IViewPort.Redo), null, Key.Y, CommandModifier);

      void AddConsoleKeyCommand(Key key, ConsoleKey consoleKey) {
         KeyBindings.Add(new KeyBinding {
            Gesture = new KeyGesture(key),
            Command = new StubCommand {
               CanExecute = ICommandExtensions.CanAlwaysExecute,
               Execute = arg => (ViewPort as IEditableViewPort)?.Edit(consoleKey)
            }
         });
      }

      AddConsoleKeyCommand(Key.Back, ConsoleKey.Backspace);
      AddConsoleKeyCommand(Key.Escape, ConsoleKey.Escape);
      AddConsoleKeyCommand(Key.Enter, ConsoleKey.Enter);
      AddConsoleKeyCommand(Key.Tab, ConsoleKey.Tab);

      DataContextChanged += (sender, e) => ClearTooltip();
      SizeChanged += (sender, e) => {
         UpdateViewPortSize();
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      };
      // WPF's OnPreviewKeyDown: a tunnelling handler in Avalonia.
      AddHandler(KeyDownEvent, (sender, e) => ClearTooltip(), RoutingStrategies.Tunnel);
   }

   public static KeyModifiers CommandModifier { get; } =
      OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

   protected override void OnPointerPressed(PointerPressedEventArgs e) {
      base.OnPointerPressed(e);
      if (ViewPort == null) return;
      var properties = e.GetCurrentPoint(this).Properties;

      if (properties.IsXButton1Pressed && ViewPort.Back.CanExecute(null)) {
         ViewPort.Back.Execute();
         return;
      }
      if (properties.IsXButton2Pressed && ViewPort.Forward.CanExecute(null)) {
         ViewPort.Forward.Execute();
         return;
      }
      downPoint = ControlCoordinatesToModelCoordinates(e);
      if (properties.IsMiddleButtonPressed) {
         middleButtonDown = true;
         e.Pointer.Capture(this);
         isPointerCaptured = true;
         return;
      }
      if (!properties.IsLeftButtonPressed) return;
      Focus();
      if (e.KeyModifiers == CommandModifier) {
         ViewPort.FollowLink(downPoint.X, downPoint.Y);
         return;
      }
      if (e.ClickCount == 2) {
         ViewPort.ExpandSelection(downPoint.X, downPoint.Y);
         return;
      }

      if (ViewPort is ViewPort editableViewPort) {
         var point = e.GetPosition(this);
         if (point.X < 0) {
            downPoint = new ModelPoint(0, downPoint.Y);
            editableViewPort.SelectionStart = downPoint;
            editableViewPort.SelectionEnd = new ModelPoint(editableViewPort.Width - 1, downPoint.Y);
         } else if (e.KeyModifiers == KeyModifiers.Shift) {
            editableViewPort.SelectionEnd = downPoint;
         } else {
            editableViewPort.SelectionStart = downPoint;
         }
         e.Pointer.Capture(this);
         isPointerCaptured = true;
      }
   }

   protected override void OnPointerMoved(PointerEventArgs e) {
      base.OnPointerMoved(e);
      if (ViewPort == null) return;
      var newMouseOverPoint = ControlCoordinatesToModelCoordinates(e);
      if (!newMouseOverPoint.Equals(mouseOverPoint)) {
         mouseOverPoint = newMouseOverPoint;
         var rawPoint = e.GetPosition(this);
         if ((int)(rawPoint.X / CellWidth) > ViewPort.Width - 1) {
            ClearTooltip();
         } else {
            UpdateTooltip(newMouseOverPoint);
         }
      }
      if (!isPointerCaptured) return;

      var point = e.GetPosition(this);
      var modelPoint = ControlCoordinatesToModelCoordinates(e);

      if (middleButtonDown) {
         ViewPort.ScrollValue -= modelPoint.Y - downPoint.Y;
         downPoint = modelPoint;
         return;
      }

      if (!(ViewPort is ViewPort viewPort)) return;

      if (point.X < 0) {
         viewPort.SelectionEnd = new ModelPoint(viewPort.Width - 1, modelPoint.Y);
      } else {
         viewPort.SelectionEnd = modelPoint;
      }
   }

   private void UpdateTooltip(ModelPoint newMouseOverPoint) {
      IDataFormat format;
      try {
         format = ViewPort[newMouseOverPoint.X, newMouseOverPoint.Y].Format;
      } catch (InvalidOperationException) {
         // if the user moves the mouse during reload, there's a chance that this tries to query the model runs while they're reloading.
         // if that happens, just skip the tooltip update
         ClearTooltip();
         return;
      }

      bool needClearToolTip = true;
      if (ViewPort is IViewPort) {
         var source = newMouseOverPoint;
         if (format is IDataFormatInstance dfi) source -= new ModelPoint(dfi.Position, 0);
         if (source == previousSource && tooltipIsEnabled) {
            // already set
            needClearToolTip = false;
         } else if (MakeNewToolTip(format, newMouseOverPoint)) {
            previousSource = source;
            needClearToolTip = false;
         }
      }
      if (needClearToolTip) {
         ClearTooltip();
      }
   }

   private bool tooltipIsEnabled;

   /// <summary>
   /// Reached on every pointer move that is not over a formatted cell, so it has to be cheap.
   /// WPF repainted the grid here; in Avalonia the tooltip is a popup that owns its own surface,
   /// so the repaint bought nothing and made the hex view flicker while the mouse moved.
   /// </summary>
   private void ClearTooltip() {
      if (!tooltipIsEnabled) return;
      tooltipIsEnabled = false;
      ToolTip.SetIsOpen(this, false);
   }

   protected override void OnPointerReleased(PointerReleasedEventArgs e) {
      base.OnPointerReleased(e);
      if (ViewPort == null) return;
      if (e.InitialPressMouseButton == MouseButton.Right && !isPointerCaptured) {
         var p = ControlCoordinatesToModelCoordinates(e);
         var children = new List<Control>();

         if (ViewPort is ViewPort editableViewPort) {
            if (!editableViewPort.IsSelected(p)) editableViewPort.SelectionStart = p;
         }
         var items = ViewPort.GetContextMenuItems(p, FileSystem);
         children.AddRange(BuildContextMenuUI(items));

         ShowMenu(children);
         return;
      }
      if (!isPointerCaptured) return;
      e.Pointer.Capture(null);
      isPointerCaptured = false;
      middleButtonDown = false;
   }

   private ModelPoint previousSource;

   protected override void OnPointerExited(PointerEventArgs e) {
      base.OnPointerExited(e);
      ToolTip.SetIsOpen(this, false);
   }

   private bool MakeNewToolTip(IDataFormat instance, ModelPoint newMouseOverPoint) {
      var visitor = new ToolTipContentVisitor(ViewPort.ModelFor(newMouseOverPoint));
      instance.Visit(visitor, default);
      if (visitor.Content.Count == 0) {
         ToolTip.SetIsOpen(this, false);
         return false;
      }
      // WPF replaced the whole ToolTip object to avoid text changing as the old one faded out.
      // Avalonia has no such fade, and replacing the tip recreates a popup window - once per
      // cell the pointer crosses - so the control is reused and only its DataContext changes.
      if (ToolTip.GetTip(this) is HexContentToolTip existing) {
         existing.DataContext = visitor.Content;
      } else {
         ToolTip.SetTip(this, new HexContentToolTip { DataContext = visitor.Content });
      }
      tooltipIsEnabled = true;
      ToolTip.SetIsOpen(this, true);
      return true;
   }

   private IEnumerable<MenuItem> BuildContextMenuUI(IReadOnlyList<IContextItem> items) {
      foreach (var item in items) {
         if (item is ContextItemGroup group) {
            var menuItem = new MenuItem { Header = group.Text };
            var subItems = new List<MenuItem>();
            foreach (var subItem in BuildContextMenuUI(group)) subItems.Add(subItem);
            menuItem.ItemsSource = subItems;
            yield return menuItem;
         } else if (item is CompositeContextItem composite) {
            foreach (var subItem in BuildContextMenuUI(composite)) {
               yield return subItem;
            }
         } else {
            var menuItem = new MenuItem {
               Header = item.Text,
               CommandParameter = item.Parameter ?? FileSystem,
               Command = item.Command
            };
            // WPF set InputGestureText (a string); Avalonia takes a KeyGesture.
            if (!string.IsNullOrEmpty(item.ShortcutText)) {
               try { menuItem.InputGesture = KeyGesture.Parse(item.ShortcutText); } catch (Exception) { /* not a gesture Avalonia can parse */ }
            }
            yield return menuItem;
         }
      }
   }

   protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
      base.OnPointerWheelChanged(e);
      if (ViewPort == null) return;
      var delta = Math.Sign(e.Delta.Y);
      if (e.KeyModifiers == CommandModifier) {
         FontSize = Math.Min(Math.Max(8, FontSize + delta), 24);
      } else if (e.KeyModifiers == KeyModifiers.Shift) {
         ViewPort.ScrollValue -= delta * 5;
      } else {
         ViewPort.ScrollValue -= delta;
      }
      e.Handled = true;
   }

   public override void Render(DrawingContext drawingContext) {
      base.Render(drawingContext);
      if (ViewPort == null) return;
      var visitor = new FormatDrawer(drawingContext, ViewPort, ViewPort.Width, ViewPort.Height, CellWidth, CellHeight, FontSize, SearchBytes);

      // clear
      drawingContext.DrawRectangle(Brush(nameof(CoreTheme.Background)), null, new Rect(0, 0, Bounds.Width, Bounds.Height));

      using (ShowHorizontalScroll ? drawingContext.PushTransform(Matrix.CreateTranslation(-HorizontalScrollValue, 0)) : default) {
         RenderBackground(drawingContext);
         RenderGrid(drawingContext);
         RenderSelection(drawingContext);
         using (drawingContext.PushClip(new Rect(new Size(ViewPort.Width * CellWidth, ViewPort.Height * CellHeight)))) {
            RenderData(visitor);
         }
      }
   }

   private static IBrush Brush(string name) => ThemeDictionary.Brush(name);

   private void RenderBackground(DrawingContext context) {
      for (int y = 0; y < ViewPort.Height; y++) {
         var format = ViewPort[0, y].Format;
         if (format is Anchor anchor) format = anchor.OriginalFormat;
         if (!(format is SpriteDecorator sprite)) continue;
         if (sprite.CellWidth != ViewPort.Width) continue;
         var source = PixelImage.WriteOnce(sprite.Pixels);
         if (source == null) continue;
         var (w, h) = (CellWidth, CellHeight);
         var rect = new Rect(0, y * h, w * sprite.CellWidth, h * sprite.CellHeight);
         if (sprite.CellWidth != sprite.Pixels.PixelWidth / 8 || sprite.CellHeight != sprite.Pixels.PixelHeight / 8) {
            // the sprite is to be displayed within the cell area, but need not be displayed with exactly one tile per cell.
            var availableRows = Math.Min(ViewPort.Height - y, sprite.CellHeight);
            var scale = Math.Max(sprite.Pixels.PixelWidth / (sprite.CellWidth * CellWidth), sprite.Pixels.PixelHeight / (availableRows * CellHeight));
            scale = 1 / scale;
            var width = sprite.Pixels.PixelWidth * scale;
            var height = sprite.Pixels.PixelHeight * scale;
            var availableWidth = ViewPort.Width * CellWidth;
            var availableHeight = availableRows * CellHeight;
            // Avalonia's Rect is immutable, so the adjusted rect is rebuilt rather than mutated.
            rect = new Rect(rect.X + (availableWidth - width) / 2, rect.Y + (availableHeight - height) / 2, width, height);
         }
         using var opacity = context.PushOpacity(.4);
         context.DrawImage(source, rect);
      }
   }

   private void RenderGrid(DrawingContext drawingContext) {
      if (!ShowGrid) return;

      var gridPen = new Pen(Brush(nameof(CoreTheme.Backlight)), 1);

      for (int x = 1; x <= ViewPort.Width; x++) {
         drawingContext.DrawLine(gridPen, new ScreenPoint(CellWidth * x, 0), new ScreenPoint(CellWidth * x, CellHeight * ViewPort.Height));
      }

      for (int y = 1; y <= ViewPort.Height; y++) {
         drawingContext.DrawLine(gridPen, new ScreenPoint(0, CellHeight * y), new ScreenPoint(CellWidth * ViewPort.Width, CellHeight * y));
      }
   }

   private void RenderSelection(DrawingContext drawingContext) {
      var cellRect = new Rect(0, 0, CellWidth, CellHeight);
      ScreenPoint
         topLeft = new ScreenPoint(0, 0),
         topRight = new ScreenPoint(CellWidth, 0),
         bottomLeft = new ScreenPoint(0, CellHeight),
         bottomRight = new ScreenPoint(CellWidth, CellHeight);

      for (int x = 0; x < ViewPort.Width; x++) {
         for (int y = 0; y < ViewPort.Height; y++) {
            var element = ViewPort[x, y];
            if (element.Edited && !ViewPort.IsSelected(new ModelPoint(x, y))) {
               drawingContext.DrawRectangle(Brush(nameof(CoreTheme.EditBackground)), null, new Rect(x * CellWidth, y * CellHeight, CellWidth, CellHeight));
            }

            if (!ViewPort.IsSelected(new ModelPoint(x, y))) continue;

            using var cellTransform = drawingContext.PushTransform(Matrix.CreateTranslation(x * CellWidth, y * CellHeight));

            drawingContext.DrawRectangle(Brush(nameof(CoreTheme.Backlight)), null, cellRect);
            if (element.Edited) {
               drawingContext.DrawRectangle(Brush(nameof(CoreTheme.EditBackground)), null, cellRect);
            }
            if (!ViewPort.IsSelected(new ModelPoint(x, y - 1))) drawingContext.DrawLine(BorderPen, topLeft, topRight);
            if (!ViewPort.IsSelected(new ModelPoint(x, y + 1))) drawingContext.DrawLine(BorderPen, bottomLeft, bottomRight);
            if (!ViewPort.IsSelected(new ModelPoint(x - 1, y))) drawingContext.DrawLine(BorderPen, topLeft, bottomLeft);
            if (!ViewPort.IsSelected(new ModelPoint(x + 1, y))) drawingContext.DrawLine(BorderPen, topRight, bottomRight);
         }
      }
   }

   private void RenderData(FormatDrawer visitor) {
      for (int x = 0; x < ViewPort.Width; x++) {
         for (int y = 0; y < ViewPort.Height; y++) {
            visitor.MouseIsOverCurrentFormat = mouseOverPoint.Equals(new ModelPoint(x, y));
            var element = ViewPort[x, y];

            visitor.Position = new ModelPoint(x, y);
            element.Format.Visit(visitor, element.Value);

            if (element.Format is UnderEdit underEdit && underEdit.AutocompleteOptions != null) {
               ShowAutocompletePopup(x, y, underEdit.AutocompleteOptions);
            }
         }
      }
   }

   private void ShowAutocompletePopup(int x, int y, IReadOnlyList<AutoCompleteSelectionItem> autocompleteOptions) {
      // close any currently open menu
      if (autocompleteOptions.Count == 0) {
         if (recentMenu != null && recentMenu.IsOpen) recentMenu.IsOpen = false;
         if (ContextMenu != null) ContextMenu.Close();
         return;
      }

      var children = new List<Control>();
      foreach (var option in autocompleteOptions) {
         var button = new Button { Content = option.CompletionText };
         button.Click += (sender, e) => {
            var text = ((Button)sender).Content.ToString();
            ((ViewPort)ViewPort).Autocomplete(text);
            recentMenu.IsOpen = false;
            if (ContextMenu != null) ContextMenu.Close();
            Focus();
         };
         if (option.IsSelected) button.BorderBrush = Brush(nameof(CoreTheme.Accent));
         children.Add(button);
      }

      // reuse existing popup if possible (to prevent flickering)
      if (recentMenu == null) {
         recentMenu = new Popup { PlacementTarget = this, Placement = PlacementMode.AnchorAndGravity };
         ((ISetLogicalParent)recentMenu).SetParent(this);
      }
      recentMenu.Child = FillPopup(children);
      recentMenu.IsLightDismissEnabled = true; // WPF: StaysOpen = false
      recentMenu.VerticalOffset = (y + 1) * CellHeight;
      recentMenu.HorizontalOffset = x * CellWidth - HorizontalScrollValue;
      recentMenu.IsOpen = true;
   }

   protected override void OnGotFocus(GotFocusEventArgs e) {
      base.OnGotFocus(e);
      if (ViewPort is IEditableViewPort editableViewPort) editableViewPort.IsFocused = true;
   }

   protected override void OnLostFocus(RoutedEventArgs e) {
      base.OnLostFocus(e);
      if (ViewPort is IEditableViewPort editableViewPort) editableViewPort.IsFocused = false;
   }

   protected override void OnTextInput(TextInputEventArgs e) {
      if (ViewPort is ViewPort editableViewPort) {
         editableViewPort.Edit(e.Text);
         e.Handled = true;
      }
   }

   protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (e.Key == Key.Enter && e.KeyModifiers == CommandModifier) {
         if (ViewPort is IEditableViewPort viewPort) {
            var point = viewPort.SelectionStart;
            viewPort.FollowLink(point.X, point.Y);
            e.Handled = true;
         }
      }
   }

   private void ShowMenu(IList<Control> children) {
      if (children.Count == 0) return;

      ContextMenu = new ContextMenu { ItemsSource = children };
      ContextMenu.Open(this);
   }

   private static Control FillPopup(IList<Control> children) {
      var panel = new StackPanel { Background = Brush(nameof(CoreTheme.Background)), MinWidth = 150 };
      foreach (var child in children) panel.Children.Add(child);
      var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible, MaxHeight = 200 };
      return new Border {
         BorderBrush = Brush(nameof(CoreTheme.Accent)),
         BorderThickness = new Thickness(1),
         Child = scroll,
      };
   }

   private void UpdateViewPortSize() {
      if (ViewPort == null) return;
      if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

      // calculate the initial 3x2 cell size from the fontsize
      var sampleElement = new FormattedText("000", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), FontSize, Brushes.Transparent);
      CellHeight = Math.Ceiling(Math.Max(sampleElement.Height, sampleElement.Width * 2 / 3));
      CellWidth = Math.Ceiling(CellHeight * 3 / 2);

      // let the ViewPort decide its width based on the available space for cells per line
      ViewPort.Width = (int)(Bounds.Width / CellWidth);
      ViewPort.Height = (int)(Bounds.Height / CellHeight);

      // add extra width to the cells as able
      var extraWidth = Math.Min(Bounds.Width - ViewPort.Width * CellWidth, ViewPort.Width * CellWidth * 2);
      if (extraWidth > 0 && ViewPort.StretchData) CellWidth += (int)(extraWidth / ViewPort.Width);

      // add horizontal scrolling if needed
      var requiredSize = ViewPort.Width * CellWidth;
      if (requiredSize > Bounds.Width) {
         ShowHorizontalScroll = true;
         HorizontalScrollMaximum = requiredSize - Bounds.Width;
         HorizontalScrollValue = Math.Min(HorizontalScrollValue, HorizontalScrollMaximum);
         var desiredThumbWidth = Bounds.Width / requiredSize;
         DesiredHorizontalViewportSize = HorizontalScrollMaximum * desiredThumbWidth / (1 - desiredThumbWidth);
      } else {
         ShowHorizontalScroll = false;
         HorizontalScrollValue = 0;
      }
   }

   private ModelPoint ControlCoordinatesToModelCoordinates(PointerEventArgs e) {
      var point = e.GetPosition(this);
      point = new ScreenPoint(Math.Max(0, point.X + HorizontalScrollValue), point.Y); // out of bounds to the left clamps to 0 (useful for row headers)
      return new ModelPoint(Math.Min((int)(point.X / CellWidth), ViewPort.Width - 1), (int)(point.Y / CellHeight)); // out of bounds right clamps to Width - 1 (prevents weird multiline scrolling.)
   }
}
