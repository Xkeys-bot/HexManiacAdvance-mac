using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace HavenSoft.HexManiac.AvaloniaUI.Implementations;

// from https://stackoverflow.com/questions/5259729/wpf-gridsplitter-replaces-binding-on-row-height-property
public class DoubleGridLengthConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return new GridLength(System.Convert.ToDouble(value));
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      var gridLength = (GridLength)value;
      return gridLength.Value;
   }
}
