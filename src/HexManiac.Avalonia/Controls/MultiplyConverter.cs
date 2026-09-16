using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

public class MultiplyConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is int number && parameter is double param) return number * param;
      // XAML numeric literals arrive as string in Avalonia's untyped ConverterParameter.
      if (value is int n2 && parameter is string text && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var p2)) return n2 * p2;
      return 0.0;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
   }
}
