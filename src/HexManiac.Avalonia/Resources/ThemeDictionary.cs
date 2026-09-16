using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// Builds the live theme dictionary from Core's Theme, the way HexManiac.WPF's
/// App.UpdateThemeDictionary does. Each theme colour lands twice: "Primary" as an IBrush and
/// "PrimaryColor" as a Color, so bindings and drawing code can each take what they need.
///
/// WPF kept this on App and swapped MergedDictionaries[0]. It lives here so that
/// DesignerThemeResource can fill a dictionary before an App exists.
/// </summary>
public static class ThemeDictionary {
   public static IReadOnlyList<string> Sources { get; } = new List<string> {
      nameof(Theme.Primary),
      nameof(Theme.Secondary),
      nameof(Theme.Background),
      nameof(Theme.Backlight),
      nameof(Theme.Error),
      nameof(Theme.Text1),
      nameof(Theme.Text2),
      nameof(Theme.Data1),
      nameof(Theme.Data2),
      nameof(Theme.Accent),
      nameof(Theme.Stream1),
      nameof(Theme.Stream2),
      nameof(Theme.EditBackground),
   };

   public static void Fill(IResourceDictionary dictionary, Theme theme) {
      var themeType = theme.GetType();
      foreach (var source in Sources) {
         var rawValue = (string)themeType.GetProperty(source).GetValue(theme);
         if (!TryParseColor(rawValue, out var color)) continue;
         dictionary[source] = new SolidColorBrush(color).ToImmutable();
         dictionary[source + "Color"] = color;
      }
   }

   public static bool TryParseColor(string text, out Color color) {
      color = default;
      if (string.IsNullOrWhiteSpace(text)) return false;
      return Avalonia.Media.Color.TryParse(text, out color);
   }

   public static IBrush Brush(string name) {
      if (Avalonia.Application.Current == null) return null;
      if (Avalonia.Application.Current.TryGetResource(name, Avalonia.Application.Current.ActualThemeVariant, out var resource)) {
         return resource as IBrush;
      }
      return null;
   }

   public static Color? ColorFor(string name) {
      if (Avalonia.Application.Current == null) return null;
      if (Avalonia.Application.Current.TryGetResource(name + "Color", Avalonia.Application.Current.ActualThemeVariant, out var resource)) {
         return resource as Color?;
      }
      return null;
   }
}
