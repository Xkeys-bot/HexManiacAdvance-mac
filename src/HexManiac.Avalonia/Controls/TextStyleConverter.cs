using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

public enum TextStle { Normal, Reminder }

/// <summary>
/// Extracts the 'style' of the text by looking at the text.
/// </summary>
public class TextStyleConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is not string str) return value;
      if (str.StartsWith("(") && str.EndsWith(")")) return TextStle.Reminder;
      return TextStle.Normal;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
   }
}

/// <summary>
/// Avalonia has no DataTrigger, so HexContentToolTip switches the "Reminder" look with a style
/// class. This exposes TextStyleConverter's result as the boolean that class binding needs.
/// </summary>
public class IsReminderConverter : IValueConverter {
   static readonly TextStyleConverter inner = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      Equals(inner.Convert(value, typeof(TextStle), parameter, culture), TextStle.Reminder);

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}
