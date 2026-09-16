using Avalonia.Controls.Primitives;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

public static class Extensions {
   /// <summary>
   /// WPF reached Popup's private Reposition() by reflection to re-run placement after the
   /// anchor moved. Avalonia recomputes placement when the popup opens, so closing and
   /// reopening is the supported equivalent.
   /// </summary>
   public static void Reposition(this Popup popup) {
      if (!popup.IsOpen) return;
      popup.IsOpen = false;
      popup.IsOpen = true;
   }
}
