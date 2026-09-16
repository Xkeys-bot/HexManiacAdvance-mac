using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HavenSoft.HexManiac.AvaloniaUI.Implementations;

/// <summary>
/// Looks a Theme property name (e.g. "Primary", "Data1") up in the application's live theme
/// dictionary. WPF read Application.Current.Resources.MergedDictionaries[0][text] directly;
/// Avalonia resources are looked up through TryGetResource.
/// </summary>
public class ThemeConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var text = value?.ToString();
      if (text == null) return null;
      if (Application.Current == null) return null;
      if (Application.Current.TryGetResource(text, Application.Current.ActualThemeVariant, out var resource)) {
         return resource as IBrush;
      }
      return null;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
   }
}
