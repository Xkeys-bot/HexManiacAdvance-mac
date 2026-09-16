using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/AngleMenuItem.xaml(.cs).
/// The WPF .xaml was an empty shell declaring only the base type, so the Avalonia port is
/// code-only; the visual template lives in GeneralResources.axaml, as it did in WPF.
/// DependencyProperty becomes StyledProperty.
/// </summary>
public class AngleMenuItem : MenuItem {

   #region AngleDirection

   public static readonly StyledProperty<AngleDirection> DirectionProperty =
      AvaloniaProperty.Register<AngleMenuItem, AngleDirection>(nameof(Direction), AngleDirection.None);

   public AngleDirection Direction {
      get => GetValue(DirectionProperty);
      set => SetValue(DirectionProperty, value);
   }

   #endregion

   #region LeftTop

   public static readonly StyledProperty<Point> LeftTopProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(LeftTop), new Point(0, 0));

   public Point LeftTop {
      get => GetValue(LeftTopProperty);
      set => SetValue(LeftTopProperty, value);
   }

   public static Point GetLeftTop(AvaloniaObject obj) => obj.GetValue(LeftTopProperty);
   public static void SetLeftTop(AvaloniaObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

   #endregion

   #region LeftMiddle

   public static readonly StyledProperty<Point> LeftMiddleProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(LeftMiddle), new Point(0, 5));

   public Point LeftMiddle {
      get => GetValue(LeftMiddleProperty);
      set => SetValue(LeftMiddleProperty, value);
   }

   public static Point GetLeftMiddle(AvaloniaObject obj) => obj.GetValue(LeftMiddleProperty);
   public static void SetLeftMiddle(AvaloniaObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

   #endregion

   #region LeftBottom

   public static readonly StyledProperty<Point> LeftBottomProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(LeftBottom), new Point(0, 10));

   public Point LeftBottom {
      get => GetValue(LeftBottomProperty);
      set => SetValue(LeftBottomProperty, value);
   }

   public static Point GetLeftBottom(AvaloniaObject obj) => obj.GetValue(LeftBottomProperty);
   public static void SetLeftBottom(AvaloniaObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

   #endregion

   #region RightTop

   public static readonly StyledProperty<Point> RightTopProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(RightTop), new Point(0, 0));

   public Point RightTop {
      get => GetValue(RightTopProperty);
      set => SetValue(RightTopProperty, value);
   }

   public static Point GetRightTop(AvaloniaObject obj) => obj.GetValue(RightTopProperty);
   public static void SetRightTop(AvaloniaObject obj, Point value) => obj.SetValue(RightTopProperty, value);

   #endregion

   #region RightMiddle

   public static readonly StyledProperty<Point> RightMiddleProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(RightMiddle), new Point(0, 5));

   public Point RightMiddle {
      get => GetValue(RightMiddleProperty);
      set => SetValue(RightMiddleProperty, value);
   }

   public static Point GetRightMiddle(AvaloniaObject obj) => obj.GetValue(RightMiddleProperty);
   public static void SetRightMiddle(AvaloniaObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

   #endregion

   #region RightBottom

   public static readonly StyledProperty<Point> RightBottomProperty =
      AvaloniaProperty.Register<AngleMenuItem, Point>(nameof(RightBottom), new Point(0, 10));

   public Point RightBottom {
      get => GetValue(RightBottomProperty);
      set => SetValue(RightBottomProperty, value);
   }

   public static Point GetRightBottom(AvaloniaObject obj) => obj.GetValue(RightBottomProperty);
   public static void SetRightBottom(AvaloniaObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

   #endregion
}
