using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

public enum AngleDirection {
   None,
   Left,
   Out,
   In,
   Right,
}

/// <summary>
/// Ported from HexManiac.WPF/Controls/AngleTextBox.xaml(.cs).
///
/// WPF wired GotKeyboardFocus/LostKeyboardFocus/MouseEnter/MouseLeave/GotFocus/LostFocus in the
/// .xaml; Avalonia's equivalents (GotFocus/LostFocus/PointerEntered/PointerExited) are attached
/// in the constructor, and the visual template still comes from GeneralResources.axaml.
/// </summary>
public class AngleTextBox : ContentControl {

   private static readonly Thickness TextContentThickness = new(0, 1, 0, 1);
   private ContextMenu menu;

   private bool keepTextBoxForContextMenu;
   public TextBox GetTextBox() {
      keepTextBoxForContextMenu = true;
      UpdateFieldTextBox(true);
      return Content as TextBox;
   }

   private IBinding textBinding;
   public IBinding TextBinding {
      get => textBinding;
      set {
         textBinding = value;
         RefreshProxy();
      }
   }

   #region AngleDirection

   public static readonly StyledProperty<AngleDirection> DirectionProperty =
      AvaloniaProperty.Register<AngleTextBox, AngleDirection>(nameof(Direction), AngleDirection.None);

   public AngleDirection Direction {
      get => GetValue(DirectionProperty);
      set => SetValue(DirectionProperty, value);
   }

   #endregion

   #region LeftTop

   public static readonly StyledProperty<Point> LeftTopProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(LeftTop), new Point(0, 0));

   public Point LeftTop {
      get => GetValue(LeftTopProperty);
      set => SetValue(LeftTopProperty, value);
   }

   public static Point GetLeftTop(AvaloniaObject obj) => obj.GetValue(LeftTopProperty);
   public static void SetLeftTop(AvaloniaObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

   #endregion

   #region LeftMiddle

   public static readonly StyledProperty<Point> LeftMiddleProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(LeftMiddle), new Point(0, 5));

   public Point LeftMiddle {
      get => GetValue(LeftMiddleProperty);
      set => SetValue(LeftMiddleProperty, value);
   }

   public static Point GetLeftMiddle(AvaloniaObject obj) => obj.GetValue(LeftMiddleProperty);
   public static void SetLeftMiddle(AvaloniaObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

   #endregion

   #region LeftBottom

   public static readonly StyledProperty<Point> LeftBottomProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(LeftBottom), new Point(0, 10));

   public Point LeftBottom {
      get => GetValue(LeftBottomProperty);
      set => SetValue(LeftBottomProperty, value);
   }

   public static Point GetLeftBottom(AvaloniaObject obj) => obj.GetValue(LeftBottomProperty);
   public static void SetLeftBottom(AvaloniaObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

   #endregion

   #region RightTop

   public static readonly StyledProperty<Point> RightTopProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(RightTop), new Point(0, 0));

   public Point RightTop {
      get => GetValue(RightTopProperty);
      set => SetValue(RightTopProperty, value);
   }

   public static Point GetRightTop(AvaloniaObject obj) => obj.GetValue(RightTopProperty);
   public static void SetRightTop(AvaloniaObject obj, Point value) => obj.SetValue(RightTopProperty, value);

   #endregion

   #region RightMiddle

   public static readonly StyledProperty<Point> RightMiddleProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(RightMiddle), new Point(0, 5));

   public Point RightMiddle {
      get => GetValue(RightMiddleProperty);
      set => SetValue(RightMiddleProperty, value);
   }

   public static Point GetRightMiddle(AvaloniaObject obj) => obj.GetValue(RightMiddleProperty);
   public static void SetRightMiddle(AvaloniaObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

   #endregion

   #region RightBottom

   public static readonly StyledProperty<Point> RightBottomProperty =
      AvaloniaProperty.Register<AngleTextBox, Point>(nameof(RightBottom), new Point(0, 10));

   public Point RightBottom {
      get => GetValue(RightBottomProperty);
      set => SetValue(RightBottomProperty, value);
   }

   public static Point GetRightBottom(AvaloniaObject obj) => obj.GetValue(RightBottomProperty);
   public static void SetRightBottom(AvaloniaObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

   #endregion

   public AngleTextBox() {
      Focusable = true;
      // Activate on enter/focus, deactivate on exit/blur. WPF could just re-read IsMouseOver
      // inside the handler; in Avalonia IsPointerOver/IsFocused are not reliably updated by the
      // time the event runs, so the direction is passed in explicitly.
      GotFocus += (sender, e) => UpdateFieldTextBox(true, takeFocus: true);
      LostFocus += (sender, e) => UpdateFieldTextBox(false);
      PointerEntered += (sender, e) => UpdateFieldTextBox(true);
      // Swapping Content moves the visual out from under the cursor, which makes Avalonia raise
      // PointerExited immediately -- and re-entering swaps again, so the two fight forever and the
      // TextBox never survives a click. Only deactivate when the pointer is really outside us.
      PointerExited += (sender, e) => {
         var p = e.GetPosition(this);
         var stillInside = p.X >= 0 && p.Y >= 0 && p.X < Bounds.Width && p.Y < Bounds.Height;
         if (!stillInside) UpdateFieldTextBox(false);
      };
      // A click on the proxy has to reach us even though the proxy is what was hit.
      AddHandler(PointerPressedEvent, (sender, e) => UpdateFieldTextBox(true, takeFocus: true), RoutingStrategies.Tunnel);
      RefreshProxy();
   }

   /// <summary>
   /// TextBlock is a lot faster than TextBox.
   /// And we only ever really need to have the focus in a single textbox at a time.
   /// Therefore, for performance reasons, we'd rather have all the non-active textboxes just
   /// *look* like TextBoxes, and really be TextBlocks instead.
   /// </summary>
   private void UpdateFieldTextBox(bool activating, bool takeFocus = false) {
      var isActive = activating || IsPointerOver || IsFocused || IsKeyboardFocusWithin || keepTextBoxForContextMenu;
      isActive |= Content is TextBox tb && tb.ContextMenu != null && tb.ContextMenu.IsOpen;
      if (isActive && Content is TextBoxLookAlike) {
         var textBox = new TextBox {
            UndoLimit = 0,
            BorderThickness = TextContentThickness,
            VerticalAlignment = VerticalAlignment.Stretch,
         };
         if (ContextMenu != null) {
            menu = ContextMenu;
            ContextMenu = null;
         }
         if (menu != null) {
            textBox.ContextMenu = menu;
            menu.Opening += (s, args) => keepTextBoxForContextMenu = true;
            menu.Closed += (s, args) => keepTextBoxForContextMenu = false;
         }

         if (DataContext is FieldArrayElementViewModel) textBox.KeyBindings.Add(
            new KeyBinding {
               Gesture = new KeyGesture(Key.Enter),
               Command = new MethodCommand(DataContext, nameof(FieldArrayElementViewModel.Accept))
            });
         if (textBinding != null) {
            textBox.Bind(TextBox.TextProperty, textBinding);
         } else {
            // Avalonia's TextBox pushes Text back on every keystroke, which is what WPF's
            // UpdateSourceTrigger=PropertyChanged was asking for.
            textBox.Bind(TextBox.TextProperty, new Binding(nameof(FieldArrayElementViewModel.Content)));
         }
         textBox.Bind(ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
         Content = textBox;
         if (takeFocus || IsKeyboardFocusWithin) {
            textBox.AttachedToVisualTree += HandleTextboxLoaded;
         } else {
            Focusable = false;
         }
      } else if (!isActive && Content is TextBox box && !box.IsKeyboardFocusWithin) {
         RefreshProxy();
      }
   }

   private void RefreshProxy() {
      var proxy = new TextBoxLookAlike { BorderThickness = TextContentThickness, VerticalAlignment = VerticalAlignment.Stretch };
      if (textBinding != null) proxy.Text.Bind(TextBlock.TextProperty, textBinding);
      proxy.Text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
      Content = proxy;
      Focusable = true;
   }

   private void HandleTextboxLoaded(object sender, EventArgs e) {
      var textBox = (TextBox)sender;
      textBox.Focus();
      Focusable = false;
   }
}
