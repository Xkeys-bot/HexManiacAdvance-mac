using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HavenSoft.HexManiac.AvaloniaUI.Implementations;

public class BooleanConverter : IValueConverter {
   public object True { get; set; }

   public object False { get; set; }

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? True : False;

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value?.Equals(True) ?? false;
}
