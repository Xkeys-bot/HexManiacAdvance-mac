using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// WPF's MenuItem exposed the shortcut as a ready-made string (InputGestureText); Avalonia
/// exposes the KeyGesture itself, so this renders it for the menu's right-hand column.
/// KeyGesture.ToString() already prints Cmd rather than Ctrl on macOS.
/// </summary>
public class GestureTextConverter : IValueConverter {
   public static GestureTextConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      value is KeyGesture gesture ? gesture.ToString() : value?.ToString() ?? string.Empty;

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}
