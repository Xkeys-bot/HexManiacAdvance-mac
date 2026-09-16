using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Implementations;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

// TODO jump button for enums
// TODO jump button for bit arrays
// TODO jump button behavior
// TODO text selection for textboxes
// TODO text editing for textboxes
// TODO application commands (cut/copy/paste/selectall) for textboxes
// TODO keyboard shortcuts cut/copy/paste/selectall for textboxes
// TODO home/end/left/right/increment/decrement for textboxes

// TODO text editing for enums
// TODO showing filtered dropdown for enums

// additional controls:
// tuples
// calculated fields

/// <summary>
/// Ported from HexManiac.WPF/Controls/TableGroupPanel.cs.
///
/// This is an immediate-mode control: one Render pass draws every table row through an
/// IGroupControl per element type. The port is mostly mechanical -- FrameworkElement becomes
/// Control, OnRender becomes Render, Push/Pop become using scopes, mouse events become pointer
/// events, and WriteableBitmap work goes through PixelBuffer (Skia has no Bgr555).
///
/// Upstream carries ~390 lines of commented-out WPF scratch code (a TableGroupMouseEditor /
/// TableGroupKeyEditor sketch) between the key handling and SpriteCache. That block is dead and
/// is not reproduced here.
/// </summary>
public class TableGroupPanel : Control {
   private readonly SpriteCache spriteCache = new();
   private readonly DispatcherTimer timer;
   private bool isCursorShowing;

   private IArrayElementViewModel keyboardFocusElement, mouseHoverElement;
   private bool isPointerCaptured;

   private readonly Dictionary<IArrayElementViewModel, IGroupControl> controls = new();

   #region Source

   public static readonly StyledProperty<ObservableCollection<IArrayElementViewModel>> SourceProperty =
      AvaloniaProperty.Register<TableGroupPanel, ObservableCollection<IArrayElementViewModel>>("Source");

   public ObservableCollection<IArrayElementViewModel> Source {
      get => GetValue(SourceProperty);
      set => SetValue(SourceProperty, value);
   }

   static TableGroupPanel() {
      SourceProperty.Changed.AddClassHandler<TableGroupPanel>((self, e) => self.OnSourceChanged(e));
      FocusableProperty.OverrideDefaultValue<TableGroupPanel>(true);
   }

   protected virtual void OnSourceChanged(AvaloniaPropertyChangedEventArgs e) {
      if (e.OldValue is ObservableCollection<IArrayElementViewModel> oldSource) {
         oldSource.CollectionChanged -= CollectionChanged;
         foreach (var element in oldSource) element.PropertyChanged -= CollectionPropertyChanged;
         controls.Clear();
      }
      if (e.NewValue is ObservableCollection<IArrayElementViewModel> newSource) {
         newSource.CollectionChanged += CollectionChanged;
         foreach (var element in newSource) {
            element.PropertyChanged += CollectionPropertyChanged;
            controls[element] = BuildControl(element);
         }
      }
      InvalidateMeasure();
      InvalidateVisual();
   }

   private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
      if (e.Action.IsAny(NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove)) {
         foreach (IArrayElementViewModel item in e.OldItems) {
            item.PropertyChanged -= CollectionPropertyChanged;
            controls.Remove(item);
         }
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace)) {
         foreach (IArrayElementViewModel item in e.NewItems) {
            item.PropertyChanged += CollectionPropertyChanged;
            controls[item] = BuildControl(item);
         }
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Move)) {
         controls.Clear();
         foreach (var element in Source) {
            element.PropertyChanged -= CollectionPropertyChanged;
            element.PropertyChanged += CollectionPropertyChanged;
            controls[element] = BuildControl(element);
         }
      }
      InvalidateMeasure();
      InvalidateVisual();
   }

   private void CollectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (sender is SpriteElementViewModel sprite) spriteCache.NeedsRedraw(sprite);
      InvalidateVisual();
   }

   #endregion

   private Point cursorPosition = new(double.NaN, double.NaN);
   public Point CursorPosition {
      get => cursorPosition;
      set {
         cursorPosition = value;
         if (double.IsNaN(cursorPosition.X)) {
            timer.Stop();
            InvalidateVisual();
         } else {
            isCursorShowing = true;
            timer.Start();
         }
      }
   }

   public int FontSize { get; private set; } = 16; // TODO styled property?

   public TableGroupPanel() {
      RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
      VerticalAlignment = VerticalAlignment.Top;
      ClipToBounds = true;
      Focusable = true;
      timer = new DispatcherTimer(TimeSpan.FromSeconds(.6), DispatcherPriority.ApplicationIdle, BlinkCursor);
   }

   protected override void OnLostFocus(RoutedEventArgs e) {
      base.OnLostFocus(e);
      if (keyboardFocusElement != null) {
         keyboardFocusElement = null;
         InvalidateVisual();
      }
      CursorPosition = new(double.NaN, double.NaN);
   }

   public override void Render(DrawingContext dc) {
      base.Render(dc);
      dc.DrawRectangle(Brush(nameof(CoreTheme.Background)), null, new Rect(0, 0, Bounds.Width, Bounds.Height));
      if (Source == null) return;
      var offset = 0;
      var context = new RenderContext(dc) { CurrentFontSize = FontSize };
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         if (!controls.TryGetValue(element, out var control)) continue;
         control.Render(context);
         offset += control.Height;
      }

      if (isCursorShowing && !double.IsNaN(CursorPosition.X)) {
         dc.DrawLine(new Pen(Brush(nameof(CoreTheme.Secondary)), 1), CursorPosition, new Point(cursorPosition.X, cursorPosition.Y + FontSize));
      }
   }

   protected override Size MeasureOverride(Size availableSize) {
      // how big I want to be
      if (Source == null) return new Size(100, 0);
      int width = (int)availableSize.Width, height = 0;
      if (width <= 0 || width > 100000) width = 100;
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         if (!controls.TryGetValue(element, out var control)) continue;
         height += control.UpdateHeight(width, height, FontSize);
      }
      return new Size(Math.Max(width, 100), height);
   }

   protected override Size ArrangeOverride(Size finalSize) {
      // how big do I actually get to be
      if (Source == null) return new Size(Math.Max(finalSize.Width, 100), 0);
      int width = (int)base.ArrangeOverride(finalSize).Width, height = 0;
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         if (!controls.TryGetValue(element, out var control)) continue;
         height += control.UpdateHeight(width, height, FontSize);
      }
      return new Size(Math.Max(width, 100), height);
   }

   #region Mouse

   private IArrayElementViewModel GetElementUnderCursor(int y) {
      foreach (var member in Source) {
         if (member is SplitterArrayElementViewModel || member.Visible) {
            if (controls.TryGetValue(member, out var control)) y -= control.Height;
         }
         if (y < 0) return member;
      }
      return null;
   }

   protected override void OnPointerPressed(PointerPressedEventArgs e) {
      base.OnPointerPressed(e);
      if (Source == null) return;
      var pos = e.GetPosition(this);
      mouseHoverElement = GetElementUnderCursor((int)pos.Y);
      keyboardFocusElement = mouseHoverElement;
      if (mouseHoverElement == null) return;
      Focus();
      controls[mouseHoverElement].MouseDown(this, e);
      e.Handled = true;
      e.Pointer.Capture(this);
      isPointerCaptured = true;
      InvalidateVisual();
   }

   protected override void OnPointerMoved(PointerEventArgs e) {
      base.OnPointerMoved(e);
      if (Source == null) return;
      if (!isPointerCaptured) {
         var pos = e.GetPosition(this);
         var previousHoverElement = mouseHoverElement;
         mouseHoverElement = GetElementUnderCursor((int)pos.Y);
         if (previousHoverElement != mouseHoverElement) {
            if (previousHoverElement != null && controls.TryGetValue(previousHoverElement, out var previous)) previous.MouseExit(this, e);
            if (mouseHoverElement != null) controls[mouseHoverElement].MouseEnter(this, e);
         }
         if (mouseHoverElement != null) controls[mouseHoverElement].MouseMove(this, e);
      } else {
         if (mouseHoverElement != null) controls[mouseHoverElement].MouseMove(this, e);
      }
   }

   protected override void OnPointerReleased(PointerReleasedEventArgs e) {
      base.OnPointerReleased(e);
      if (!isPointerCaptured) return;
      if (mouseHoverElement != null && controls.TryGetValue(mouseHoverElement, out var control)) control.MouseUp(this, e);
      e.Pointer.Capture(null);
      isPointerCaptured = false;
   }

   protected override void OnPointerExited(PointerEventArgs e) {
      base.OnPointerExited(e);
      if (mouseHoverElement != null) {
         if (controls.TryGetValue(mouseHoverElement, out var control)) control.MouseExit(this, e);
         mouseHoverElement = null;
      }
   }

   #endregion

   protected override void OnTextInput(TextInputEventArgs e) {
      base.OnTextInput(e);
      if (keyboardFocusElement == null) return;
      controls[keyboardFocusElement].TextInput(this, e);
      e.Handled = true;
   }

   protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (keyboardFocusElement == null) return;
      if (e.Key == Key.Tab) {
         var index = Source.IndexOf(keyboardFocusElement);
         if (index != -1) {
            index += 1;
            if (e.KeyModifiers == KeyModifiers.Shift) index -= 2;
            if (index < 0) index += Source.Count;
            if (index >= Source.Count) index -= Source.Count;
            keyboardFocusElement = Source[index];
            e.Handled = true;
            InvalidateVisual();
            // TODO some sort of "gained focus" notification for the GroupControl?
            return;
         }
      }
      controls[keyboardFocusElement].KeyInput(this, e);
   }

   private static IBrush Brush(string name) => ThemeDictionary.Brush(name);

   private IGroupControl BuildControl(IArrayElementViewModel element) {
      IGroupControl control = element switch {
         SplitterArrayElementViewModel splitter => new GroupSplitterControl(splitter),
         FieldArrayElementViewModel field => new GroupTextControl(field),
         ComboBoxArrayElementViewModel combo => new GroupEnumControl(combo),
         BitListArrayElementViewModel bits => new GroupBitArrayControl(bits),
         SpriteElementViewModel sprite => new GroupImageControl(sprite, spriteCache),
         SpriteIndicatorElementViewModel spriteIndicator => new GroupSpriteIndicatorControl(spriteIndicator, spriteCache),
         PaletteElementViewModel palette => new GroupPaletteControl(palette),
         OffsetRenderViewModel offsetRender => new GroupOffsetRenderControl(offsetRender, spriteCache),
         TextStreamElementViewModel textStream => new GroupTextStreamControl(textStream),
         ButtonArrayElementViewModel button => new GroupButtonControl(button),
         PythonButtonElementViewModel pButton => new GroupPythonButtonControl(pButton),
         _ => new GroupDefaultControl(element)
      };

      return control;
   }

   private void BlinkCursor(object sender, EventArgs e) {
      isCursorShowing = !isCursorShowing;
      InvalidateVisual();
   }
}

public class SpriteCache {
   // sprites are expensive, but need to be updated often
   // Re-use the same WriteableBitmap when possible to save resources.

   private readonly List<WriteableBitmap> wbCache = new();
   private readonly List<IPixelViewModel> pvmCache = new();
   private long cacheNeedsRedraw = 0;

   public WriteableBitmap WriteUpdate(IPixelViewModel viewModel) {
      var cacheIndex = pvmCache.IndexOf(viewModel);
      if (cacheIndex >= 0 && (cacheNeedsRedraw & (1L << cacheIndex)) == 0) return wbCache[cacheIndex];

      var pixels = viewModel.PixelData;
      if (pixels == null) return null;
      var expectedLength = viewModel.PixelWidth * viewModel.PixelHeight;
      if (pixels.Length < expectedLength || pixels.Length == 0) return null;

      if (cacheIndex < 0) {
         // image not found, need to make one
         var source = PixelBuffer.EnsureSize(null, viewModel.PixelWidth, viewModel.PixelHeight);
         PixelBuffer.Write555(source, pixels, viewModel.PixelWidth, viewModel.PixelHeight);
         wbCache.Add(source);
         pvmCache.Add(viewModel);
         if (wbCache.Count > 64) { wbCache.RemoveAt(0); pvmCache.RemoveAt(0); cacheNeedsRedraw >>= 1; }
         return source;
      } else if (wbCache[cacheIndex].PixelSize.Width != viewModel.PixelWidth || wbCache[cacheIndex].PixelSize.Height != viewModel.PixelHeight) {
         // size is wrong, throw out the cache and replace it
         var source = PixelBuffer.EnsureSize(null, viewModel.PixelWidth, viewModel.PixelHeight);
         PixelBuffer.Write555(source, pixels, viewModel.PixelWidth, viewModel.PixelHeight);
         wbCache[cacheIndex] = source;
         return source;
      } else {
         // size is right, just redraw over the same WriteableBitmap to save resources.
         PixelBuffer.Write555(wbCache[cacheIndex], pixels, viewModel.PixelWidth, viewModel.PixelHeight);
         cacheNeedsRedraw &= ~(1L << cacheIndex);
         return wbCache[cacheIndex];
      }
   }

   public void NeedsRedraw(IPixelViewModel sprite) {
      var cacheIndex = pvmCache.IndexOf(sprite);
      if (cacheIndex >= 0) cacheNeedsRedraw |= 1L << cacheIndex;
   }
}

public record RenderContext(DrawingContext Api) {
   public static Typeface Consolas { get; } = new Typeface("Consolas");

   public int DefaultTextPadding => 4;
   public int CurrentFontSize { get; set; } = 16;
   public Pen AccentPen { get; } = new Pen(Brush(nameof(CoreTheme.Accent)), 1);

   public static IBrush Brush(string name) {
      if (string.IsNullOrEmpty(name)) return null;
      return ThemeDictionary.Brush(name);
   }

   public void DrawRectangle(string fill, string stroke, int x, int y, int width, int height) {
      var borderPen = string.IsNullOrEmpty(stroke) ? null : new Pen(Brush(stroke), 1);
      Api.DrawRectangle(Brush(fill), borderPen, new Rect(x, y, width, height));
   }

   public void DrawIcon(Rect placement, string icon, string fill, string border = null, double borderThickness = 1) {
      var pen = border != null ? new Pen(Brush(border), borderThickness) : null;
      var geometry = IconExtension.GetIcon(icon);
      if (geometry == null) return;
      var widthRatio = placement.Width / geometry.Bounds.Width;
      var heightRatio = placement.Height / geometry.Bounds.Height;
      using var transform = Api.PushTransform(
         Matrix.CreateScale(widthRatio, heightRatio) *
         Matrix.CreateTranslation(placement.X - geometry.Bounds.Left * widthRatio, placement.Y - geometry.Bounds.Top * heightRatio));
      Api.DrawGeometry(Brush(fill), pen, geometry);
   }

   public void DrawText(Point origin, double size, string text, string foreground)
      => Api.DrawText(FormattedText(text, size, foreground), origin);

   public void DrawText(Point origin, double preferredSize, double maxWidth, string text, string foreground) {
      var formattedText = FormattedText(text, preferredSize, foreground);
      if (formattedText.Width > maxWidth) formattedText = FormattedText(text, preferredSize * maxWidth / formattedText.Width, foreground);
      Api.DrawText(formattedText, origin);
   }

   public void DrawCheckbox(int x, int y, int size, bool isChecked, bool isHover) {
      var border = isHover ? nameof(CoreTheme.Accent) : nameof(CoreTheme.Secondary);
      var fill = nameof(CoreTheme.Backlight);
      Api.DrawRectangle(Brush(fill), new Pen(Brush(border), 1), new Rect(x, y, size, size));
      if (isChecked) {
         DrawIcon(new Rect(x + 1, y - 2, size - 2, size + 2), nameof(Icons.Check), nameof(CoreTheme.Accent));
      }
   }

   public void DrawTextButton(Rect rect, double fontSize, string text, bool isHover, bool isEnabled = true) {
      var border = isHover ? nameof(CoreTheme.Primary) : nameof(CoreTheme.Secondary);
      var fill = nameof(CoreTheme.Backlight);
      var textFill = nameof(CoreTheme.Primary);
      if (!isEnabled) { fill = nameof(CoreTheme.Background); border = nameof(CoreTheme.Secondary); textFill = nameof(CoreTheme.Secondary); }
      Api.DrawRectangle(Brush(fill), new Pen(Brush(border), 1), rect);
      var formattedText = FormattedText(text, fontSize, textFill);
      if (formattedText.Width > rect.Width - 2) {
         fontSize *= (rect.Width - 2) / formattedText.Width;
         formattedText = FormattedText(text, fontSize, nameof(CoreTheme.Primary));
      }
      Api.DrawText(formattedText, new Point(rect.X + rect.Width / 2 - formattedText.Width / 2, rect.Y));
   }

   /// <summary>
   /// Draws a squarish jump button based on the fontSize.
   /// </summary>
   public void DrawJumpButton(Point start, bool enabled, bool hover) {
      int width = CurrentFontSize, height = CurrentFontSize;
      using var transform = Api.PushTransform(Matrix.CreateTranslation(start.X, start.Y));
      var background = enabled ? nameof(CoreTheme.Backlight) : nameof(CoreTheme.Background);
      var border = (hover && enabled) ? nameof(CoreTheme.Primary) : nameof(CoreTheme.Secondary);

      var content = $"M0,0 L {width - 4},0 {width},{height / 2} {width - 4},{height} 0,{height} 4,{height / 2} Z";
      Api.DrawGeometry(Brush(background), new Pen(Brush(border), 1), Geometry.Parse(content));

      content = $"M4,3 L {width - 6},3 {width - 4},{height / 2} {width - 6},{height - 3} 4,{height - 3} 6,{height / 2} Z";
      Api.DrawGeometry(Brush(border), null, Geometry.Parse(content));
   }

   public static FormattedText FormattedText(string text, double size, string foreground)
      => new FormattedText(text ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Consolas, Math.Max(size, 1), Brush(foreground));

   public static double GetDesiredFontSize(string text, double defaultSize, double maxWidth) {
      var formattedText = FormattedText(text, defaultSize, null);
      if (formattedText.Width <= maxWidth) return defaultSize;
      return defaultSize * maxWidth / formattedText.Width;
   }
}

/// <summary>
/// Represents a lightweight set of methods for rendering and interacting with a section of a table panel
/// </summary>
public interface IGroupControl {
   int YOffset { get; }
   int Width { get; }
   int Height { get; }
   int UpdateHeight(int availableWidth, int currentHeight, int fontSize);

   void Render(RenderContext context);

   void MouseEnter(TableGroupPanel parent, PointerEventArgs e);
   void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e);
   void MouseMove(TableGroupPanel parent, PointerEventArgs e); // if the mouse is down, MouseMove will continue activating on whatever element got clicked.
   void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e);
   void MouseExit(TableGroupPanel parent, PointerEventArgs e);

   void TextInput(TableGroupPanel parent, TextInputEventArgs e);
   void KeyInput(TableGroupPanel parent, KeyEventArgs e);
}

public abstract record GroupFixedHeighteControl() {
   public int YOffset { get; private set; }
   public int Width { get; private set; }
   public int Height { get; protected set; } = 20;
   public virtual int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      Width = availableWidth;
      YOffset = currentHeight;
      Height = fontSize + 4;
      return Height;
   }

   // helpers
   protected static string Primary { get; } = nameof(CoreTheme.Primary);
   protected static string Accent { get; } = nameof(CoreTheme.Accent);
   protected static string Secondary { get; } = nameof(CoreTheme.Secondary);
   protected static string Background { get; } = nameof(CoreTheme.Background);
   protected static string Backlight { get; } = nameof(CoreTheme.Backlight);
}

public record GroupDefaultControl(IArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public void Render(RenderContext context) {
      context.DrawText(new Point(2, YOffset), context.CurrentFontSize, Element.GetType().Name, Primary);
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupTextControl(FieldArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   enum ControlSegment { None, Label, TextBox, Button }
   private ControlSegment mouseOver;

   public bool IsFocused { get; private set; }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) {
      if (string.IsNullOrEmpty(Element.Content)) return;
      var text = RenderContext.FormattedText(Element.Content, parent.FontSize, null);
      var labelAndContentWidth = Width - parent.FontSize - 2;
      var leftEdge = labelAndContentWidth - text.Width - 2;
      var characterWidth = text.Width / Element.Content.Length;

      var p = e.GetPosition(parent);
      var index = (p.X - leftEdge) / characterWidth + .5;
      index = (int)index.LimitToRange(0, Element.Content.Length);
      parent.CursorPosition = new Point((int)(leftEdge + index * characterWidth), YOffset + 1);
   }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) {
      var p = e.GetPosition(parent);
      parent.Cursor = p.X > Width / 2 ? new Cursor(StandardCursorType.Ibeam) : Cursor.Default;

      if (e.GetCurrentPoint(parent).Properties.IsLeftButtonPressed) {
         // TODO mouse drag
      } else {
         // mouse hover
         var newHover = CalculateMouseOver(parent, new Point(p.X, p.Y - YOffset));
         if ((newHover == ControlSegment.Button) != (mouseOver == ControlSegment.Button)) {
            mouseOver = newHover;
            parent.InvalidateVisual(); // redraw for border
         }
      }
   }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) {
      // TODO end of mouse drag
      // TODO need the ability to actually respond to these application commands
      if (e.InitialPressMouseButton == MouseButton.Right) {
         // WPF bound these to ApplicationCommands, which Avalonia has no equivalent for; upstream
         // notes they don't actually do anything yet either.
         parent.ContextMenu = new ContextMenu {
            ItemsSource = new List<Control> {
               new MenuItem { Header = "Cut" },
               new MenuItem { Header = "Copy" },
               new MenuItem { Header = "Paste" },
               new Separator(),
               new MenuItem { Header = "Select All" },
            },
         };
      }
   }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) {
      parent.Cursor = Cursor.Default;
      if (mouseOver == ControlSegment.Button) {
         mouseOver = ControlSegment.None;
         parent.InvalidateVisual();
      }
   }
   private ControlSegment CalculateMouseOver(TableGroupPanel parent, Point internalPoint) {
      var labelAndContentWidth = Width - parent.FontSize;
      if (internalPoint.X < labelAndContentWidth / 2) return ControlSegment.Label;
      if (internalPoint.X > labelAndContentWidth) return ControlSegment.Button;
      return ControlSegment.TextBox;
   }

   public void Render(RenderContext context) {
      var topOfText = YOffset;
      var labelAndContentWidth = Width - context.CurrentFontSize - 2;

      // label
      context.DrawText(new Point(2, topOfText + 2), context.CurrentFontSize - 4, labelAndContentWidth / 2, Element.Name, Primary);

      // box
      var pen = IsFocused ? context.AccentPen : null;
      var background = RenderContext.Brush(Backlight);
      context.Api.DrawRectangle(background, pen, new Rect(labelAndContentWidth / 2, YOffset + 1, labelAndContentWidth / 2, Height - 2));

      // content
      var text = RenderContext.FormattedText(Element.Content, context.CurrentFontSize, Primary);
      var textWidth = text.Width + 2;
      if (textWidth > labelAndContentWidth / 2) textWidth = labelAndContentWidth / 2; // TODO crop the text if this happens
      context.Api.DrawText(text, new Point(labelAndContentWidth - textWidth, topOfText));

      // goto button
      context.DrawJumpButton(new Point(Width - context.CurrentFontSize, topOfText + 2), Element.CanAccept(), mouseOver == ControlSegment.Button);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupSplitterControl(SplitterArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool hover;
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) => Height = base.UpdateHeight(availableWidth, currentHeight, fontSize) * 2;

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { hover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { Element.ToggleVisibility.Execute(); parent.InvalidateVisual(); }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { hover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      var collapserRect = new Rect(4, YOffset + Height * 11 / 16, Height / 2 - 8, Height / 4 - 4);
      var border = hover ? Accent : null;
      if (Element.Visible) {
         context.DrawIcon(collapserRect, nameof(Icons.Chevron), Primary, border, .2);
      } else {
         context.DrawIcon(collapserRect, nameof(Icons.ChevronUp), Primary, border, .2);
      }
      context.DrawText(new Point(Height / 2, YOffset + Height / 2), context.CurrentFontSize, Width - Height / 2, Element.SectionName, Primary);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupImageControl(SpriteElementViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private int scale;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var multiple = (int)Math.Ceiling((double)Element.PixelHeight / unitHeight);
      if (multiple < 5) {
         scale = 2;
         multiple = (int)Math.Ceiling((double)Element.PixelHeight * 2 / unitHeight);
      } else {
         scale = 1;
      }

      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element);
      if (image == null) return;
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.PixelWidth * scale, Element.PixelHeight * scale));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupEnumControl(ComboBoxArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void Render(RenderContext context) {
      // name
      context.DrawText(new Point(2, YOffset + 2), context.CurrentFontSize - 4, Width / 2, Element.Name, Primary);

      // box
      context.Api.DrawRectangle(RenderContext.Brush(Backlight), new Pen(RenderContext.Brush(Secondary), 1), new Rect(Width / 2, YOffset + 1, Width / 2, Height - 2));
      var textSize = RenderContext.GetDesiredFontSize(Element.FilteringComboOptions.DisplayText, context.CurrentFontSize, Width / 2 - context.CurrentFontSize - 2);
      context.DrawText(new Point(Width / 2 + 2, YOffset + 1), textSize, Element.FilteringComboOptions.DisplayText, Primary);
      var unit = context.CurrentFontSize / 4;
      context.DrawIcon(new Rect(Width - unit * 4, YOffset + unit + 2, unit * 4 - 2, unit * 2), nameof(Icons.Chevron), Primary);

      // TODO jump button
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupOffsetRenderControl(OffsetRenderViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private double yStart = double.NaN;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var multiple = (int)Math.Ceiling((double)Element.PixelHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) => parent.Cursor = new Cursor(StandardCursorType.Hand);
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) => yStart = e.GetPosition(parent).Y;
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) {
      if (double.IsNaN(yStart)) return;
      var newY = e.GetPosition(parent).Y;
      var delta = (int)(newY - yStart);
      yStart += delta;
      Cache.NeedsRedraw(Element);
      Element.ShiftDelta(0, delta);
   }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) => yStart = double.NaN;
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) => parent.Cursor = Cursor.Default;

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element);
      if (image == null) return;
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.PixelWidth, Element.PixelHeight));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupSpriteIndicatorControl(SpriteIndicatorElementViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private double scale = 1;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      scale = Math.Min(1, (double)availableWidth / Element.Image.PixelWidth);
      var multiple = (int)Math.Ceiling(Element.Image.PixelHeight * scale / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element.Image);
      if (image == null) return;
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.Image.PixelWidth * scale, Element.Image.PixelHeight * scale));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupTextStreamControl(TextStreamElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);

      var formattedContent = RenderContext.FormattedText(Element.Content, fontSize, Primary);
      var minHeight = formattedContent.Height + 4;

      var multiple = (int)Math.Ceiling((double)minHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void Render(RenderContext context) {
      context.DrawRectangle(Backlight, Secondary, 0, YOffset, Width, Height);
      context.DrawText(new Point(2, YOffset + 2), context.CurrentFontSize, Element.Content, Primary);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupPaletteControl(PaletteElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var blockHeight = Element.Colors.ColorHeight * (fontSize * 2 / 3 + 4);
      var multiple = (int)Math.Ceiling((double)blockHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { }

   public void Render(RenderContext context) {
      var colorWidth = Element.Colors.ColorWidth;
      var unitWidth = context.CurrentFontSize * 2 / 3 + 4;
      for (int y = 0; y < Element.Colors.ColorHeight; y++) {
         for (int x = 0; x < colorWidth; x++) {
            var color = Element.Colors.Elements[y * colorWidth + x];
            var fill = new SolidColorBrush(TileImage.Convert16BitColor(color.Color));
            var border = RenderContext.Brush(Primary);
            context.Api.DrawRectangle(fill, new Pen(border, 1), new Rect(unitWidth * x + 1, unitWidth * y + 1 + YOffset, unitWidth - 2, unitWidth - 2));
         }
      }
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }

   private int GetCell(TableGroupPanel parent, double x, double y) {
      var unitWidth = parent.FontSize * 2 / 3 + 4;
      var cellY = (int)((y - YOffset) / unitWidth).LimitToRange(0, Element.Colors.ColorHeight);
      var cellX = (int)(x / unitWidth).LimitToRange(0, Element.Colors.ColorWidth);
      return cellY * Element.Colors.ColorWidth + cellX;
   }
}

public record GroupBitArrayControl(BitListArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private int unitHeight, unitWidth, childrenPerLine;
   private BitElement hover, mouseClickElement;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      if (Element.Count == 0) return Height = unitHeight;
      var characterLength = Element.Max(child => child.BitLabel.Length);
      var sampleText = RenderContext.FormattedText(new string('X', characterLength), fontSize - 4, null);
      unitWidth = (int)(sampleText.Width + fontSize + 4);
      childrenPerLine = Math.Max(availableWidth / Math.Max(unitWidth, 1), 1);
      var rows = (Element.Count - 1) / childrenPerLine + 1;
      return Height = unitHeight * (rows + 1);
   }
   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) => hover = null;
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) => mouseClickElement = GetBit(e.GetPosition(parent));
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) {
      var newHover = GetBit(e.GetPosition(parent));
      if (newHover != hover) parent.InvalidateVisual();
      hover = newHover;
   }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) {
      if (e.InitialPressMouseButton == MouseButton.Right) {
         parent.ContextMenu = new ContextMenu {
            ItemsSource = new List<Control> {
               new MenuItem { Header = "Select All", Command = Element.SelectAll },
               new MenuItem { Header = "Unselect All", Command = Element.UnselectAll },
            },
         };
      } else if (GetBit(e.GetPosition(parent)) == mouseClickElement && mouseClickElement != null) {
         mouseClickElement.IsChecked = !mouseClickElement.IsChecked;
      }
   }
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) => hover = null;

   public void Render(RenderContext context) {
      context.DrawText(new Point(0, YOffset + 2), context.CurrentFontSize, Element.Name, Primary);

      for (int i = 0; i < Element.Count; i++) {
         var xOffset = (i % childrenPerLine) * unitWidth;
         var yOffset = (i / childrenPerLine + 1) * unitHeight + YOffset;
         context.DrawCheckbox(xOffset + 2, yOffset + context.CurrentFontSize / 4, context.CurrentFontSize * 3 / 4, Element[i].IsChecked, Element[i] == hover);
         xOffset += context.CurrentFontSize + 2;
         context.DrawText(new Point(xOffset, yOffset + 4), context.CurrentFontSize - 4, Element[i].BitLabel, Primary);
      }
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }

   private BitElement GetBit(Point point) {
      // Avalonia's Point is immutable, so the Y adjustment makes a new one.
      var p = new Point(point.X, point.Y - YOffset - unitHeight);
      if (p.Y < 0) return null;
      if (childrenPerLine < 1 || unitHeight < 1 || unitWidth < 1) return null;
      var rows = (Element.Count - 1) / childrenPerLine + 1;
      var yy = (int)(p.Y / unitHeight).LimitToRange(0, rows - 1);
      var xx = (int)(p.X / unitWidth).LimitToRange(0, childrenPerLine - 1);
      var hover = yy * childrenPerLine + xx;
      if (hover >= Element.Count) {
         return null;
      } else {
         return Element[hover];
      }
   }
}

public record GroupButtonControl(ButtonArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool isHover;
   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { isHover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) => Element.Command.Execute();
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { isHover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      context.DrawTextButton(new Rect(2, YOffset + 2, Width - 4, Height - 4), context.CurrentFontSize - 2, Element.Text, isHover, Element.Command.CanExecute(null));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}

public record GroupPythonButtonControl(PythonButtonElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool isHover;
   public void MouseEnter(TableGroupPanel parent, PointerEventArgs e) { isHover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, PointerPressedEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, PointerEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, PointerReleasedEventArgs e) => Element.Execute();
   public void MouseExit(TableGroupPanel parent, PointerEventArgs e) { isHover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      context.DrawTextButton(new Rect(2, YOffset + 2, Width - 4, Height - 4), context.CurrentFontSize - 2, Element.Name, isHover, Element.CanExecute());
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextInputEventArgs e) { }
}
